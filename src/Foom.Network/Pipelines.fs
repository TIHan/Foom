[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network

let ReliableOrderedAckReceiver (packetPool : PacketPool) (ackManager : AckManager) ack =
    let mutable nextSeqId = 0us

    fun (packets : Packet seq) callback ->

        packets
        |> Seq.iter (fun packet ->
            if nextSeqId = packet.SequenceId then
                callback packet
                ack nextSeqId
                nextSeqId <- nextSeqId + 1us
            else
                ackManager.MarkCopy packet
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

[<Struct>]
type Data = { bytes : byte []; startIndex : int; size : int }

let createMergeFilter (packetPool : PacketPool) =
    let packets = ResizeArray ()
    fun data callback ->
        data
        |> Seq.iter (fun data ->
            if packets.Count = 0 then
                let packet = packetPool.Get ()
                if packet.LengthRemaining >= data.size then
                    packet.WriteRawBytes (data.bytes, data.startIndex, data.size)
                    packets.Add packet
                else
                    let count = (data.size / packet.LengthRemaining) + (if data.size % packet.LengthRemaining > 0 then 1 else 0)
                    let mutable startIndex = data.startIndex
                    failwith "yopac"
                    
            else
                let packet = packets.[packets.Count - 1]
                if packet.LengthRemaining >= data.size then
                    packet.WriteRawBytes (data.bytes, data.startIndex, data.size)
                else
                    let packet = packetPool.Get ()
                    if packet.LengthRemaining >= data.size then
                        packet.WriteRawBytes (data.bytes, data.startIndex, data.size)
                    else
                        failwith "too big"

                    packets.Add packet
        )

        packets |> Seq.iter callback
        packets.Clear ()

let createReliableOrderedFilter (ackManager : AckManager) =
    let sequencer = Sequencer ()
    fun (packets : Packet seq) callback ->
        packets
        |> Seq.iter (fun packet ->
            sequencer.Assign packet
            packet.PacketType <- PacketType.ReliableOrdered
            ackManager.MarkCopy packet
            callback packet
        )

let createClientReceiveFilter () =
    fun (packets : Packet seq) callback ->
        packets |> Seq.iter callback

module Sender =

    let createUnreliable packetPool =
        let mergeFilter = createMergeFilter packetPool
        Pipeline.create ()
        |> Pipeline.filter mergeFilter
        |> Pipeline.build

    let createReliableOrdered packetPool =
        let ackManager = AckManager ()
        let mergeFilter = createMergeFilter packetPool
        let reliableOrderedFilter = createReliableOrderedFilter ackManager
        Pipeline.create ()
        |> Pipeline.filter mergeFilter
        |> Pipeline.filter reliableOrderedFilter
        |> Pipeline.build

module Receiver =

    let createUnreliable () =
        let receiveFilter = createClientReceiveFilter ()
        Pipeline.create ()
        |> Pipeline.filter receiveFilter
        |> Pipeline.build