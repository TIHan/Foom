namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type UnreliableChannel (endPoint: IUdpEndPoint) as this =

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))

    let outgoingQueue = Queue<Packet> ()
    let pendingQueue = Queue<Packet> ()

    let rec enqueueData data startIndex size =

        let packet = packetPool.Pop ()

        let sizeRemaining = packet.Raw.Length - packet.Length
        if sizeRemaining < size then
            failwith "Message is bigger than the size of an unreliable packet. Consider using a sequenced packet instead."
            //enqueueData data startIndex sizeRemaining
            //enqueueData data (startIndex + sizeRemaining) (size - sizeRemaining)
        else

        packet.SetData (PacketType.Unreliable, data, startIndex, size)

        if pendingQueue.Count > 1 then failwith "Pending Queue shouldn't have more than 1."
        if pendingQueue.Count = 1 then
            let peekPacket = pendingQueue.Peek ()
            if peekPacket.Raw.Length - peekPacket.Length > packet.Length then
                peekPacket.Merge packet
                this.RecyclePacket packet
            else
                outgoingQueue.Enqueue (pendingQueue.Dequeue ())
                pendingQueue.Enqueue packet
        else
            pendingQueue.Enqueue packet

    member this.EnqueueData (data, startIndex, size) =
        enqueueData data startIndex size

    member this.Process f =
        if pendingQueue.Count > 1 then failwith "Pending Queue shouldn't have more than 1."

        if pendingQueue.Count = 1 then
            outgoingQueue.Enqueue (pendingQueue.Dequeue ())

        while outgoingQueue.Count > 0 do
            let packet = outgoingQueue.Dequeue ()
            f packet
            this.RecyclePacket packet

        let x = ()
        ()

    member this.RecyclePacket (packet : Packet) =
        packet.Reset ()
        packetPool.Push packet
