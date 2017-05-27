[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network
open Foom.Network.Pipeline

let ReceiverSource (packetPool : PacketPool) =

    { new ISource with 

        member x.Send (data, startIndex, size, output) =
            let packet = packetPool.Get ()
            packet.Set (data, startIndex, size)
            output packet }

let ReliableOrderedAckReceiver (packetPool : PacketPool) (ackManager : AckManager) ack =

    let mutable nextSeqId = 0us
    let queue = Queue<Packet> ()

    { new IFilter with

        member x.Send packet =
            if nextSeqId = packet.SequenceId then
                queue.Enqueue packet
                nextSeqId <- nextSeqId + 1us
            else
                ackManager.MarkCopy packet

        member x.Process output =
            while queue.Count > 0 do
                let packet = queue.Dequeue ()
                ack packet.SequenceId
                output packet

            ackManager.ForEachPending (fun seqId copyPacket ->
                if int nextSeqId = seqId then
                    let packet = packetPool.Get ()
                    copyPacket.CopyTo packet
                    ackManager.Ack seqId
                    ack nextSeqId
                    nextSeqId <- nextSeqId + 1us
                    output packet
            ) }

module NewPipeline =

    open NewPipeline

    let ReliableOrderedAckReceiver (packetPool : PacketPool) (ackManager : AckManager) ack =
        let mutable nextSeqId = 0us

        Filter (fun (packets : Packet seq) callback ->

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
        )

let ReliableAckReceiver (packetPool : PacketPool) ack =

    let queue = Queue<Packet> ()

    { new IFilter with

        member x.Send packet =
            queue.Enqueue packet

        member x.Process output =
            while queue.Count > 0 do
                let packet = queue.Dequeue ()
                ack packet.SequenceId
                output packet
    }

let ReliableSequencedAckReceiver (packetPool : PacketPool) ack =

    let mutable recentFrom = 0us
    let mutable recentTo = 0us
    let queue = Queue<Packet> ()

    { new IFilter with

        member x.Send packet =

            // A new message has been received.
            if sequenceMoreRecent packet.SequenceId recentTo then

                while queue.Count > 0 do
                    packetPool.Recycle (queue.Dequeue ())

                recentFrom <- packet.SequenceId
                recentTo <- packet.SequenceId + uint16 packet.Fragments
                queue.Enqueue packet

            // The packet could be a fragment of the entire message.
            elif sequenceMoreRecent packet.SequenceId recentFrom then
                if packet.FragmentId > 0us || packet.Fragments > 0uy then
                    queue.Enqueue packet
                else
                    failwith "shouldn't happen yet"
                    packetPool.Recycle packet

            else
                packetPool.Recycle packet
            
        member x.Process output =
            while queue.Count > 0 do
                let packet = queue.Dequeue ()
                ack packet.SequenceId
                output packet
    }

[<Struct>]
type Data = { bytes : byte []; startIndex : int; size : int }

let createMergeFilter (packetPool : PacketPool) =
        let packets = ResizeArray ()
        NewPipeline.Filter (fun data callback ->
            data
            |> Seq.iter (fun data ->
                if packets.Count = 0 then
                    let packet = packetPool.Get ()
                    if packet.LengthRemaining >= data.size then
                        packet.WriteRawBytes (data.bytes, data.startIndex, data.size)
                        packets.Add packet
                    else
                        let count = (data.size / packet.LengthRemaining) - (if data.size % packet.LengthRemaining > 0 then -1 else 0)
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
        )

let createClientReceiveFilter () =
    NewPipeline.Filter (fun (packets : Packet seq) callback ->
        packets |> Seq.iter callback
    )

let unreliableSender packetPool =
    let mergeFilter = createMergeFilter packetPool
    NewPipeline.createPipeline mergeFilter
    |> NewPipeline.build

let basicReceiver packetPool =
    let receiveFilter = createClientReceiveFilter ()
    NewPipeline.createPipeline receiveFilter
    |> NewPipeline.build

let reliableOrderedReceiver ack f =
    let packetPool = PacketPool 64
    let ackManager = AckManager ()

    create (ReceiverSource packetPool)
    |> add (ReliableOrderedAckReceiver packetPool ackManager ack)
    |> sink (fun packet ->
        f packet
        packetPool.Recycle packet
    )
