[<AutoOpen>]
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

[<AbstractClass>]
type Receiver () =

    abstract Receive : TimeSpan * Packet -> unit

    abstract Update : TimeSpan -> unit

type ReliableOrderedReceiver (packetPool : PacketPool, sendAck, receive) =
    inherit Receiver ()

    let ackManager = AckManager (TimeSpan.FromSeconds 1.)

    let mutable nextSeqId = 0us

    override this.Receive (time, packet) =
        if packet.Type = PacketType.ReliableOrdered then
            if nextSeqId = packet.SequenceId then
                receive packet
                sendAck nextSeqId
                nextSeqId <- nextSeqId + 1us
            else
                ackManager.MarkCopy (packet, time)
                packetPool.Recycle packet

    override this.Update time =
        ackManager.ForEachPending (fun seqId copyPacket ->
            if int nextSeqId = seqId then
                let packet = packetPool.Get ()
                copyPacket.CopyTo packet
                ackManager.Ack seqId
                sendAck nextSeqId
                nextSeqId <- nextSeqId + 1us
                receive packet
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