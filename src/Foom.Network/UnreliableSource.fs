namespace Foom.Network

open System

type UnreliableSource (packetPool : PacketPool) =

    interface ISource with 

        member this.Send (data, startIndex, size, output) =
            let packet = packetPool.Get ()

            if size > packet.LengthRemaining then
                failwith "Unreliable data is larger than what a new packet can hold. Consider using reliable sequenced."

            packet.SetData (data, startIndex, size)
            output packet
