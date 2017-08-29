namespace Foom.Network

open System
open System.Collections.Generic

open Foom.Network

type Sender2 private (packetPool : PacketPool, packetQueue : Queue<Packet>, dataMerger : DataMerger, output : TimeSpan -> Packet -> (Packet -> unit) -> unit) =

    let enqueue = packetQueue.Enqueue

    member __.Enqueue (buffer, offset, count) =
        dataMerger.Enqueue (buffer, offset, count)

    member __.Flush time =
        dataMerger.Flush (fun packet -> output time packet enqueue)

    member __.Process f =
        while packetQueue.Count > 0 do
            let packet = packetQueue.Dequeue ()
            f packet
            packetPool.Recycle packet

    static member Create (packetPool, output) =
        Sender2 (packetPool, Queue (), DataMerger.Create packetPool, output)

    static member CreateUnreliable packetPool =
        Sender2.Create (packetPool, fun _ packet enqueue -> 
            packet.Type <- PacketType.Unreliable
            enqueue packet
        )

    static member CreateReliableOrderedAck packetPool =
        Sender2.Create (packetPool, fun _ packet enqueue -> 
            packet.Type <- PacketType.ReliableOrderedAck
            enqueue packet
        )

type Sender =
    {
        packetPool : PacketPool
        merger : DataMerger
        mergedPackets : ResizeArray<Packet>
        mutable output : TimeSpan -> Packet -> unit
        packetQueue : Queue<Packet>
    }

    member this.Enqueue (bytes, startIndex, size) =
        this.merger.Enqueue (bytes, startIndex, size)

    member this.Flush time =
        this.merger.Flush (fun packet -> this.output time packet)

    member this.Process f =
        while this.packetQueue.Count > 0 do
            let packet = this.packetQueue.Dequeue ()
            f packet
            this.packetPool.Recycle packet

    static member Create packetPool =
        {
            packetPool = packetPool
            merger = DataMerger.Create packetPool
            mergedPackets = ResizeArray ()
            output = fun _ _ -> ()
            packetQueue = Queue ()
        }

    static member CreateUnreliable packetPool =
        let sender = Sender.Create packetPool

        sender.output <- fun _ packet -> sender.packetQueue.Enqueue packet

        sender

    static member CreateReliableOrderedAck packetPool =
        let sender = Sender.Create packetPool

        sender.output <- 
            fun _ packet ->
                packet.Type <- PacketType.ReliableOrderedAck
                sender.packetQueue.Enqueue packet
        
        sender

type SenderAck =
    {
        sender : Sender
        sequencer : Sequencer
        ackManager : AckManager
    }

    member this.Enqueue (bytes, startIndex, size) =
        this.sender.Enqueue (bytes, startIndex, size)

    member this.Flush time =
        this.sender.Flush time

        this.ackManager.Update time (fun ack packet ->
            this.sender.packetQueue.Enqueue packet
        )

    member this.Ack ack =
        let packet = this.ackManager.GetPacket (int ack)
        if obj.ReferenceEquals (packet, null) |> not then 
            this.sender.packetPool.Recycle packet
        this.ackManager.Ack ack

    member this.Process f =
        while this.sender.packetQueue.Count > 0 do
            let packet = this.sender.packetQueue.Dequeue ()
            f packet

    static member Create packetPool =
        {
            sender = Sender.Create packetPool
            sequencer = Sequencer ()
            ackManager = AckManager (TimeSpan.FromSeconds 1.)
        }
     
    static member CreateReliableOrdered packetPool =
        let senderAck = SenderAck.Create packetPool

        let sequencer = senderAck.sequencer
        let ackManager = senderAck.ackManager
        let packetQueue = senderAck.sender.packetQueue

        let output = fun time packet ->
            sequencer.Assign packet
            packet.Type <- PacketType.ReliableOrdered
            ackManager.Mark (packet, time) |> ignore
            packetQueue.Enqueue packet

        senderAck.sender.output <- output

        senderAck

type Receiver =
    {
        packetPool : PacketPool
        inputQueue : Queue<Packet>
        outputQueue : Queue<Packet>

        mutable output : TimeSpan -> Packet -> unit
    }

    member this.Enqueue packet =
        this.inputQueue.Enqueue packet

    member this.Flush time =
        while this.inputQueue.Count > 0 do
            let packet = this.inputQueue.Dequeue ()
            this.output time packet

    member this.Process f =
        while this.outputQueue.Count > 0 do
            let packet = this.outputQueue.Dequeue ()
            f packet
            this.packetPool.Recycle packet
           
    static member Create packetPool =
        {
            packetPool = packetPool
            inputQueue = Queue ()
            outputQueue = Queue ()
            output = fun _ _ -> ()
        }

    static member CreateUnreliable packetPool =
        let receiver = Receiver.Create packetPool

        receiver.output <- fun _ packet -> receiver.outputQueue.Enqueue packet

        receiver

type ReceiverAck =
    {
        receiver : Receiver
        ackManager : AckManager
        fragmentAssembler : FragmentAssembler

        mutable nextSeqId : uint16
    }

    member this.Enqueue packet =
        this.receiver.Enqueue packet

    member this.Flush time =
        this.receiver.Flush time

        this.ackManager.ForEachPending (fun seqId packet ->
            if this.nextSeqId = seqId then
                this.ackManager.Ack seqId
                this.nextSeqId <- this.nextSeqId + 1us

                if packet.IsFragmented then
                    this.fragmentAssembler.Mark (packet, fun packet ->
                        this.receiver.outputQueue.Enqueue packet
                    )
                else
                    this.receiver.outputQueue.Enqueue packet
        )

    member this.Process f =
        this.receiver.Process f

    static member Create packetPool =
        {
            receiver = Receiver.Create packetPool
            ackManager = AckManager (TimeSpan.FromSeconds 1.)
            fragmentAssembler = FragmentAssembler.Create ()
            nextSeqId = 0us
        }

    static member CreateReliableOrdered packetPool =
        let receiverAck = ReceiverAck.Create packetPool

        receiverAck.receiver.output <-
            fun time packet ->
                if receiverAck.ackManager.Mark (packet, time) |> not then
                    packetPool.Recycle packet

        receiverAck
