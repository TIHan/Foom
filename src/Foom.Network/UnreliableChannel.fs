namespace Foom.Network

open System
open System.Collections.Generic

type UnreliableChannel (packetPool : PacketPool) =

    member this.ProcessData (data, startIndex, size, f) =
        let packet = packetPool.Get ()
        if size > packet.SizeRemaining then
            failwith "Unreliable data is larger than what a new packet can hold. Consider using reliable sequenced."

        packet.SetData (PacketType.Unreliable, data, startIndex, size)
        f packet
