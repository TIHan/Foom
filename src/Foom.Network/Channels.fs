﻿[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network

[<AbstractClass>]
type Channel () =

    abstract Send : byte [] * startIndex : int * size : int -> unit

    abstract Update : TimeSpan -> unit

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

[<Sealed>]
type UnreliableChannel (packetPool : PacketPool, send) =
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
            send packet.Raw packet.Length
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
            ackManager.MarkCopy (packet, time)
            send packet.Raw packet.Length
            packetPool.Recycle packet

        ackManager.Update time (fun ack copyPacket ->
            let packet = packetPool.Get ()
            copyPacket.CopyTo packet
            send packet.Raw packet.Length
            packetPool.Recycle packet
        )

        packets.Clear ()

    member this.Ack ack =
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
            send packet.Raw packet.Length
            packetPool.Recycle packet
        packets.Clear ()

[<AbstractClass>]
type Receiver () =

    abstract Receive : TimeSpan * Packet * IUdpEndPoint -> unit

    abstract Update : TimeSpan -> unit

type ConnectionAcceptedReceiver (packetPool : PacketPool, receive) =
    inherit Receiver ()

    let queue = Queue<Packet * IUdpEndPoint> ()

    override this.Receive (_, packet, endPoint) =
        System.Diagnostics.Debug.Assert (packet.Type = PacketType.ConnectionAccepted)
        queue.Enqueue (packet, endPoint)

    override this.Update _ =
        while queue.Count <> 0 do
            let packet, endPoint = queue.Dequeue ()
            receive endPoint
            packetPool.Recycle packet

type ConnectionRequestedReceiver (packetPool : PacketPool, receive) =
    inherit Receiver ()

    let queue = Queue<Packet * IUdpEndPoint> ()

    override this.Receive (_, packet, endPoint) =
        System.Diagnostics.Debug.Assert (packet.Type = PacketType.ConnectionRequested)
        queue.Enqueue (packet, endPoint)

    override this.Update _ =
        while queue.Count <> 0 do
            let packet, endPoint = queue.Dequeue ()
            receive packet endPoint
            packetPool.Recycle packet

type UnreliableReceiver (packetPool : PacketPool, receive) =
    inherit Receiver ()

    let queue = Queue<Packet * IUdpEndPoint> ()

    override this.Receive (_, packet, endPoint) =
        System.Diagnostics.Debug.Assert (packet.Type = PacketType.Unreliable)
        queue.Enqueue (packet, endPoint)

    override this.Update _ =
        while queue.Count <> 0 do
            let packet, endPoint = queue.Dequeue ()
            receive packet endPoint
            packetPool.Recycle packet

type ReliableOrderedReceiver (packetPool : PacketPool, sendAck, receive) =
    inherit Receiver ()

    let ackManager = AckManager (TimeSpan.FromSeconds 1.)
    let packetQueue = Queue<Packet> ()

    let mutable nextSeqId = 0us

    override this.Receive (time, packet, _) =
        System.Diagnostics.Debug.Assert (packet.Type = PacketType.ReliableOrdered)
        if nextSeqId = packet.SequenceId then
            packetQueue.Enqueue packet
            ackManager.Ack nextSeqId
            sendAck nextSeqId
            nextSeqId <- nextSeqId + 1us
        else
            ackManager.MarkCopy (packet, time)
            packetPool.Recycle packet

    override this.Update time =
        while packetQueue.Count <> 0 do
            let packet = packetQueue.Dequeue ()
            receive packet
            packetPool.Recycle packet

        ackManager.ForEachPending (fun seqId copyPacket ->
            if nextSeqId = seqId then
                let packet = packetPool.Get ()
                copyPacket.CopyTo packet
                ackManager.Ack nextSeqId
                sendAck nextSeqId
                nextSeqId <- nextSeqId + 1us
                receive packet
                packetPool.Recycle packet
        )


//let createMergeAckFilter (packetPool : PacketPool) =
//    let packets = ResizeArray ()
//    Pipeline.filter (fun (time : TimeSpan) data callback ->
//        data
//        |> Seq.iter (fun data ->
//            if packets.Count = 0 then
//                let packet = packetPool.Get ()
//                if packet.DataLengthRemaining >= sizeof<int> then
//                    packet.WriteInt (data.ack)
//                    packets.Add packet
//                else
//                    failwith "shouldn't happen"
                    
//            else
//                let packet = packets.[packets.Count - 1]
//                if packet.DataLengthRemaining >= sizeof<int> then
//                    packet.WriteInt (data.ack)
//                else
//                    let packet = packetPool.Get ()
//                    if packet.DataLengthRemaining >= sizeof<int> then
//                        packet.WriteInt (data.ack)
//                        packets.Add packet
//                    else
//                        failwith "shouldn't happen"

                    
//        )

//        packets |> Seq.iter callback
//        packets.Clear ()
//    )