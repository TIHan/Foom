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
                ack nextSeqId
                nextSeqId <- nextSeqId + 1us
            else
                ackManager.MarkCopy packet

        member x.Process output =
            while queue.Count > 0 do
                output (queue.Dequeue ())

            ackManager.ForEachPending (fun seqId copyPacket ->
                if int nextSeqId = seqId then
                    let packet = packetPool.Get ()
                    copyPacket.CopyTo packet
                    ackManager.Ack seqId
                    ack nextSeqId
                    nextSeqId <- nextSeqId + 1us
                    output packet
            ) }

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

let basicReceiver f =
    let packetPool = PacketPool 64
    let source = ReceiverSource packetPool

    create source
    |> sink (fun packet ->
        f packet
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