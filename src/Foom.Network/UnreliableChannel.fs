namespace Foom.Network

open System
open System.Collections.Generic

type UnreliableChannel (packetPool : PacketPool) =

    let queue = Queue<Packet> ()

    member this.SendData (data, startIndex, size) =
        let packet = packetPool.Get ()
        if size > packet.LengthRemaining then
            failwith "Unreliable data is larger than what a new packet can hold. Consider using reliable sequenced."

        packet.SetData (data, startIndex, size)
        queue.Enqueue packet
 
    member this.Flush f =
        while queue.Count > 0 do
            let packet = queue.Dequeue ()
            f packet
            packetPool.Recycle packet
