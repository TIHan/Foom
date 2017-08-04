[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network

[<Sealed>]
type DataMerger (packetPool : PacketPool) =

    let data = ResizeArray ()

    member this.Send (bytes, startIndex, size) =
        data.Add (struct (bytes, startIndex, size))

    member this.Update mergedPackets =
        for i = 0 to data.Count - 1 do
            let struct (bytes, startIndex, size) = data.[i]
            packetPool.GetFromBytes (bytes, startIndex, size, mergedPackets)
        data.Clear ()

[<AbstractClass>]
type Receiver () =

    abstract Receive : TimeSpan * Packet -> unit

    abstract Update : TimeSpan -> unit

[<AbstractClass>]
type Channel () =

    abstract Send : byte [] * startIndex : int * size : int -> unit

    abstract Update : TimeSpan -> unit

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

    let dataMerger = DataMerger packetPool
    let mergedPackets = ResizeArray ()

    override this.Send (bytes, startIndex, size) =
        dataMerger.Send (bytes, startIndex, size)

    override this.Update time =
        dataMerger.Update mergedPackets

        let packets = mergedPackets
        for i = 0 to packets.Count - 1 do
            let packet = packets.[i]
            send packet
            packetPool.Recycle packet
        packets.Clear ()
                
[<Sealed>]
type ReliableOrderedChannel (packetPool : PacketPool, send) =
    inherit Channel ()

    let dataMerger = DataMerger packetPool
    let mergedPackets = ResizeArray ()

    let sequencer = Sequencer ()
    let ackManager = AckManager (TimeSpan.FromSeconds 1.)

    override this.Send (bytes, startIndex, size) =
        dataMerger.Send (bytes, startIndex, size)

    override this.Update time =
        dataMerger.Update mergedPackets

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

    let dataMerger = DataMerger packetPool
    let mergedPackets = ResizeArray ()

    override this.Send (bytes, startIndex, size) =
        dataMerger.Send (bytes, startIndex, size)

    override this.Update time =
        dataMerger.Update mergedPackets

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
