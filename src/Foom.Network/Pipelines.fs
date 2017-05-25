[<AutoOpen>]
module Foom.Network.Pipelines

open System
open System.Collections.Generic

open Foom.Network
open Foom.Network.Pipeline

//let Defragment (packetPool : PacketPool) =

//    let packets = Array.init 65536 (fun _ -> Unchecked.defaultof<Packet>)
//    let queue = Queue<Packet> ()

//    { new IFilter with

//        member x.Send packet =
//            if packet.Fragments = 0uy && packet.FragmentId = 0us then
//                queue.Enqueue packet
//            else


//        member x.Process output =
//            while queue.Count > 0 do
//                let packet = queue.Dequeue ()
//                ack packet.SequenceId
//                output packet
//    }

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

        Filter<Packet, Packet> (fun packet packets ->

            ackManager.ForEachPending (fun seqId copyPacket ->
                if int nextSeqId = seqId then
                    let packet = packetPool.Get ()
                    copyPacket.CopyTo packet
                    ackManager.Ack seqId
                    ack nextSeqId
                    nextSeqId <- nextSeqId + 1us
                    packets.Add packet
            )

            if nextSeqId = packet.SequenceId then
                packets.Add packet
                ack nextSeqId
                nextSeqId <- nextSeqId + 1us
            else
                ackManager.MarkCopy packet
                packetPool.Recycle packet
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


let unreliableSender f =
    let packetPool = PacketPool 64
    let source = UnreliableSource packetPool
    let merger = PacketMerger packetPool

    create source
    |> add merger
    |> sink (fun packet ->
        f packet
        packetPool.Recycle packet
    )

let basicReceiver byteStream =
    let packetPool = PacketPool 64
    let source = ReceiverSource packetPool
    let byteWriter = ByteWriter byteStream

    create source
    |> sink (fun packet ->
        if packet.FragmentId > 0us then
            byteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.Size)
        else
            byteWriter.WriteRawBytes (packet.Raw, 0, packet.Length)

        packetPool.Recycle packet
    )

let reliableOrderedReceiver ack f =
    let packetPool = PacketPool 64
    let ackManager = AckManager ()

    create (ReceiverSource packetPool)
    |> add (ReliableOrderedAckReceiver packetPool ackManager ack)
    |> sink (fun packet ->
        f packet
        packetPool.Recycle packet
    )