[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network

[<Struct>]
type Data = { bytes : byte []; startIndex : int; size : int; packetType : PacketType; ack : int }

let createMergeFilter (packetPool : PacketPool) =
    let packets = ResizeArray ()
    Pipeline.filter (fun (time : TimeSpan) data callback ->
        data
        |> Seq.iter (fun data -> packetPool.GetFromBytes (data.bytes, data.startIndex, data.size, packets))

        packets |> Seq.iter callback
        packets.Clear ()
    )

let createMergeAckFilter (packetPool : PacketPool) =
    let packets = ResizeArray ()
    Pipeline.filter (fun (time : TimeSpan) data callback ->
        data
        |> Seq.iter (fun data ->
            if packets.Count = 0 then
                let packet = packetPool.Get ()
                if packet.DataLengthRemaining >= sizeof<int> then
                    packet.WriteInt (data.ack)
                    packets.Add packet
                else
                    failwith "shouldn't happen"
                    
            else
                let packet = packets.[packets.Count - 1]
                if packet.DataLengthRemaining >= sizeof<int> then
                    packet.WriteInt (data.ack)
                else
                    let packet = packetPool.Get ()
                    if packet.DataLengthRemaining >= sizeof<int> then
                        packet.WriteInt (data.ack)
                        packets.Add packet
                    else
                        failwith "shouldn't happen"

                    
        )

        packets |> Seq.iter callback
        packets.Clear ()
    )
   

module Sender =

    let createReliableOrderedFilter (packetPool : PacketPool) (ackManager : AckManager) =
        let sequencer = Sequencer ()
        Pipeline.filter (fun time (packets : Packet seq) callback ->
            packets
            |> Seq.iter (fun packet ->
                sequencer.Assign packet
                packet.Type <- PacketType.ReliableOrdered
                ackManager.MarkCopy (packet, time)
                callback packet
            )

            ackManager.Update time (fun ack copyPacket ->
                let packet = packetPool.Get ()
                copyPacket.CopyTo packet
                callback packet
            )
        )

    let createUnreliable packetPool f =
        let mergeFilter = createMergeFilter packetPool
        Pipeline.create ()
        |> mergeFilter
        |> Pipeline.sink (fun packet ->
            f packet
            packetPool.Recycle packet
        )

    let createUnreliable2 packetPool =
        let mergeFilter = createMergeFilter packetPool
        Pipeline.create ()
        |> mergeFilter

    let createReliableOrdered packetPool =
        let ackManager = AckManager (TimeSpan.FromSeconds 1.)
        let mergeFilter = createMergeFilter packetPool
        let reliableOrderedFilter = createReliableOrderedFilter packetPool ackManager
        Pipeline.create ()
        |> mergeFilter
        |> reliableOrderedFilter

    let create packetPool f =
        let unreliable = createUnreliable2 packetPool
        let reliableOrdered = createReliableOrdered packetPool

        (unreliable, reliableOrdered)
        ||> Pipeline.merge2 (fun sendUnreliable sendReliableOrdered data ->
            match data.packetType with
            | PacketType.Unreliable -> sendUnreliable data
            | PacketType.ConnectionAccepted
            | PacketType.ConnectionRequested -> sendUnreliable data
            | PacketType.ReliableOrdered -> sendReliableOrdered data

            | _ -> ()
        )
        |> Pipeline.sink (fun packet ->
            f packet
            packetPool.Recycle packet
        )

module Receiver =

    let createClientReceiveFilter () =
        Pipeline.filter (fun time (packets : Packet seq) callback ->
            packets |> Seq.iter callback
        )

    let createReliableOrderedAckReceiveFilter (packetPool : PacketPool) (ackManager : AckManager) ack =
        let mutable nextSeqId = 0us

        let f =
            fun time (packets : Packet seq) callback ->

                packets
                |> Seq.iter (fun packet ->
                    if packet.Type = PacketType.ReliableOrdered then
                        if nextSeqId = packet.SequenceId then
                            callback packet
                            ack nextSeqId
                            nextSeqId <- nextSeqId + 1us
                        else
                            ackManager.MarkCopy (packet, time)
                            packetPool.Recycle packet
                )

                ackManager.ForEachPending (fun seqId copyPacket ->
                    if int nextSeqId = seqId then
                        let packet = packetPool.Get ()
                        copyPacket.CopyTo packet
                        ackManager.Ack seqId
                        ack nextSeqId
                        nextSeqId <- nextSeqId + 1us
                        callback packet
                )
        f

    let createUnreliable (packetPool : PacketPool) f =
        let receiveFilter = createClientReceiveFilter ()
        Pipeline.create ()
        |> receiveFilter
        |> Pipeline.sink (fun packet ->
            f packet
            packetPool.Recycle packet
        )

    let createReliableOrdered packetPool (ackManager : AckManager) ack f =
        let receiveFilter = createReliableOrderedAckReceiveFilter packetPool ackManager ack

        Pipeline.create ()
        |> Pipeline.filter receiveFilter
        |> Pipeline.sink (fun packet ->
            f packet
            packetPool.Recycle packet
        )

    let createUnreliable2 (packetPool : PacketPool) =
        let receiveFilter = createClientReceiveFilter ()
        Pipeline.create ()
        |> receiveFilter

    let createReliableOrdered2 (sender : Pipeline<Data>) packetPool =
        let onAck = (fun ack -> sender.Send { bytes = [||]; startIndex = 0; size = 0; packetType = PacketType.ReliableOrderedAck; ack = int ack })
        let ackManager = AckManager (TimeSpan.FromSeconds 1.)

        let receiveFilter = createReliableOrderedAckReceiveFilter packetPool ackManager onAck

        let pipeline =
            Pipeline.create ()
            |> Pipeline.filter receiveFilter

        let ackPipeline =
            Pipeline.create ()
            |> Pipeline.filter (fun time packets callback ->
                packets
                |> Seq.iter (fun (packet : Packet) ->
                    if packet.Type = PacketType.ReliableOrderedAck then
                        packet.ReadAcks ackManager.Ack
                        callback packet
                )
            )

        (pipeline, ackPipeline)
        ||> Pipeline.merge2 (fun send sendAck packet ->
            match packet.Type with
            | PacketType.ReliableOrdered -> send packet
            | PacketType.ReliableOrderedAck -> sendAck packet
            | _ -> failwith "Invalid packet."
        )

    let create sender packetPool f =
        let unreliable = createUnreliable2 packetPool
        let reliableOrdered = createReliableOrdered2 sender packetPool

        (unreliable, reliableOrdered)
        ||> Pipeline.merge2 (fun sendUnreliable sendReliableOrdered packet ->
            match packet.Type with
            | PacketType.Unreliable -> sendUnreliable packet

            | PacketType.ReliableOrdered -> sendReliableOrdered packet

            | _ -> packetPool.Recycle packet
        )
        |> Pipeline.sink (fun packet ->
            f packet
            packetPool.Recycle packet
        )