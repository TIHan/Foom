namespace Foom.Network

open System
open System.Collections.Generic

type UnreliableChannel (packetPool : PacketPool) =

    member this.ProcessData (data, startIndex, size, f) =
        let packet = packetPool.Get ()
        packet.SetData (PacketType.Unreliable, data, startIndex, size)
        f packet
