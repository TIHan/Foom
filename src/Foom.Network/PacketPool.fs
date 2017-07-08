namespace Foom.Network

open System
open System.Collections.Generic

type PacketPool (poolAmount) =

    let pool = Stack (Array.init poolAmount (fun _ -> Packet ()))

    member this.Amount = poolAmount

    member this.Get () = pool.Pop ()

    member this.Recycle (packet : Packet) =
        packet.Reset ()
        if pool.Count + 1 > poolAmount then
            failwith "For right now, this throws an exception" 
        pool.Push packet

[<AutoOpen>]
module PacketPoolHelpers =

    let getFromBytes (bytes : byte []) (startIndex : int) (size : int) (packet : Packet) (packets : ResizeArray<Packet>) (packetPool : PacketPool) =
        let count = (size / packet.DataLengthRemaining) + (if size % packet.DataLengthRemaining > 0 then 1 else 0)
        let mutable startIndex = startIndex

        packet.FragmentId <- uint16 count
        packet.WriteRawBytes (bytes, startIndex, packet.DataLengthRemaining)
        packets.Add packet

        for i = 1 to count - 1 do
            let packet = packetPool.Get ()
            packet.FragmentId <- uint16 (count - i)

            if i = (count - 1) then
                let startIndex = startIndex + (i * packet.DataLengthRemaining)
                packet.WriteRawBytes (bytes, startIndex, size - startIndex)
            else
                packet.WriteRawBytes (bytes, startIndex + (i * packet.DataLengthRemaining), packet.DataLengthRemaining)

            packets.Add packet 

type PacketPool with

    member this.GetFromBytes (bytes, startIndex, size) =
        let packets = ResizeArray ()
        let packet = this.Get ()
        getFromBytes bytes startIndex size packet packets this
        packets

    member this.GetFromBytes (bytes, startIndex, size, packet, packets) =
        getFromBytes bytes startIndex size packet packets this