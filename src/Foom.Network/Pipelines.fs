[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network

[<Struct>]
type Data = { bytes : byte []; startIndex : int; size : int }

let fragmentPackets (packetPool : PacketPool) (packets : ResizeArray<Packet>) (packet : Packet) (data : Data) =
    let count = (data.size / packet.DataLengthRemaining) + (if data.size % packet.DataLengthRemaining > 0 then 1 else 0)
    let mutable startIndex = data.startIndex

    packet.FragmentId <- uint16 count
    packet.WriteRawBytes (data.bytes, data.startIndex, packet.DataLengthRemaining)
    packets.Add packet

    for i = 1 to count - 1 do
        let packet = packetPool.Get ()
        packet.FragmentId <- uint16 (count - i)

        if i = (count - 1) then
            let startIndex = data.startIndex + (i * packet.DataLengthRemaining)
            packet.WriteRawBytes (data.bytes, startIndex, data.size - startIndex)
        else
            packet.WriteRawBytes (data.bytes, data.startIndex + (i * packet.DataLengthRemaining), packet.DataLengthRemaining)

        packets.Add packet 
                    

let createMergeFilter (packetPool : PacketPool) =
    let packets = ResizeArray ()
    fun data callback ->
        data
        |> Seq.iter (fun data ->
            if packets.Count = 0 then
                let packet = packetPool.Get ()
                if packet.DataLengthRemaining >= data.size then
                    packet.WriteRawBytes (data.bytes, data.startIndex, data.size)
                    packets.Add packet
                else
                    fragmentPackets packetPool packets packet data
                    
            else
                let packet = packets.[packets.Count - 1]
                if packet.DataLengthRemaining >= data.size then
                    packet.WriteRawBytes (data.bytes, data.startIndex, data.size)
                else
                    let packet = packetPool.Get ()
                    if packet.DataLengthRemaining >= data.size then
                        packet.WriteRawBytes (data.bytes, data.startIndex, data.size)
                        packets.Add packet
                    else
                        fragmentPackets packetPool packets packet data

                    
        )

        packets |> Seq.iter callback
        packets.Clear ()

let createReliableOrderedFilter (ackManager : AckManager) =
    let sequencer = Sequencer ()
    fun (packets : Packet seq) callback ->
        packets
        |> Seq.iter (fun packet ->
            sequencer.Assign packet
            packet.Type <- PacketType.ReliableOrdered
            ackManager.MarkCopy (packet, TimeSpan.Zero)
            callback packet
        )

let createClientReceiveFilter () =
    fun (packets : Packet seq) callback ->
        packets |> Seq.iter callback

let createReliableOrderedAckReceiveFilter (packetPool : PacketPool) (ackManager : AckManager) ack =
    let mutable nextSeqId = 0us

    fun (packets : Packet seq) callback ->

        packets
        |> Seq.iter (fun packet ->
            if nextSeqId = packet.SequenceId then
                callback packet
                ack nextSeqId
                nextSeqId <- nextSeqId + 1us
            else
                ackManager.MarkCopy (packet, TimeSpan.Zero)
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

    let createReliableOrdered packetPool ack =
        let ackManager = AckManager ()
        let receiveFilter = createReliableOrderedAckReceiveFilter packetPool ackManager ack

        Pipeline.create ()
        |> Pipeline.filter receiveFilter
        |> Pipeline.build