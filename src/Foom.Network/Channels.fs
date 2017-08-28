[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network

[<Sealed>]
type DataMerger () =

    let data = ResizeArray ()

    member this.Enqueue (bytes, startIndex, size) =
        data.Add (struct (bytes, startIndex, size))

    member this.Flush (packetPool : PacketPool, outputPackets) =
        for i = 0 to data.Count - 1 do
            let struct (bytes, startIndex, size) = data.[i]
            packetPool.GetFromBytes (bytes, startIndex, size, outputPackets)
        data.Clear ()

[<AbstractClass>]
type Receiver () =

    abstract Receive : TimeSpan * Packet -> unit

    abstract Update : TimeSpan -> unit

[<AbstractClass>]
type Channel () =

    abstract Send : byte [] * startIndex : int * size : int -> unit

    abstract Update : TimeSpan -> unit

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

    member this.Flush (time) =
        this.merger.Flush (this.packetPool, this.mergedPackets)

        for i = 0 to this.mergedPackets.Count - 1 do
            this.output time this.mergedPackets.[i]

        this.mergedPackets.Clear ()

    member this.Process f =
        while this.packetQueue.Count > 0 do
            let packet = this.packetQueue.Dequeue ()
            f packet
            this.packetPool.Recycle packet

    static member Create packetPool =
        {
            packetPool = packetPool
            merger = DataMerger ()
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

            ackManager.Update time (fun ack packet ->
                packetQueue.Enqueue packet
            )

        senderAck.sender.output <- output

        senderAck

type UnreliableReceiver (packetPool : PacketPool, receive) =
    inherit Receiver ()

    let queue = Queue<Packet> ()

    override this.Receive (_, packet) =
        System.Diagnostics.Debug.Assert (packet.Type = PacketType.Unreliable)
        queue.Enqueue (packet)

    override this.Update _ =
        while queue.Count <> 0 do
            let packet = queue.Dequeue ()
            receive packet
            packetPool.Recycle packet

type UnreliableSender (packetPool : PacketPool, send) =
    inherit Channel ()

    let dataMerger = DataMerger ()
    let mergedPackets = ResizeArray ()

    override this.Send (bytes, startIndex, size) =
        dataMerger.Enqueue (bytes, startIndex, size)

    override this.Update time =
        dataMerger.Flush (packetPool, mergedPackets)

        let packets = mergedPackets
        for i = 0 to packets.Count - 1 do
            let packet = packets.[i]
            send packet
            packetPool.Recycle packet
        packets.Clear ()
                
[<Sealed>]
type ReliableOrderedChannel (packetPool : PacketPool, send) =
    inherit Channel ()

    let dataMerger = DataMerger ()
    let mergedPackets = ResizeArray ()

    let sequencer = Sequencer ()
    let ackManager = AckManager (TimeSpan.FromSeconds 1.)

    override this.Send (bytes, startIndex, size) =
        dataMerger.Enqueue (bytes, startIndex, size)

    override this.Update time =
        dataMerger.Flush (packetPool, mergedPackets)

        let packets = mergedPackets
        for i = 0 to packets.Count - 1 do
            let packet = packets.[i]
            sequencer.Assign packet
            packet.Type <- PacketType.ReliableOrdered
            ackManager.Mark (packet, time) |> ignore
            send packet

        ackManager.Update time (fun ack packet ->
            send packet
        )

        packets.Clear ()

    member this.Ack ack =
        let packet = ackManager.GetPacket (int ack)
        if obj.ReferenceEquals (packet, null) |> not then 
            packetPool.Recycle packet
        ackManager.Ack ack


type ReliableOrderedAckSender (packetPool : PacketPool, send) =
    inherit Channel ()

    let dataMerger = DataMerger ()
    let mergedPackets = ResizeArray ()

    override this.Send (bytes, startIndex, size) =
        dataMerger.Enqueue (bytes, startIndex, size)

    override this.Update time =
        dataMerger.Flush (packetPool, mergedPackets)

        let packets = mergedPackets
        for i = 0 to packets.Count - 1 do
            let packet = packets.[i]
            packet.Type <- PacketType.ReliableOrderedAck
            send packet
            packetPool.Recycle packet
        packets.Clear ()

type ReliableOrderedReceiver (packetPool : PacketPool, sendAck, receive) =
    inherit Receiver ()

    let ackManager = AckManager (TimeSpan.FromSeconds 1.)
    let fragmentAssembler = FragmentAssembler ()

    let mutable nextSeqId = 0us

    // TODO: AckManager needs to use the packetPool.
    override this.Receive (time, packet) =
        System.Diagnostics.Debug.Assert (packet.Type = PacketType.ReliableOrdered)
        sendAck packet.SequenceId
        if ackManager.Mark (packet, time) |> not then
            packetPool.Recycle packet

    override this.Update time =
        ackManager.ForEachPending (fun seqId packet ->
            if nextSeqId = seqId then
                ackManager.Ack seqId
                nextSeqId <- nextSeqId + 1us

                if packet.IsFragmented then
                    fragmentAssembler.Mark (packet, fun packet ->
                        receive packet
                        packetPool.Recycle packet
                    )
                else
                    receive packet
                    packetPool.Recycle packet
        )
