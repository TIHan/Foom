namespace Foom.Network

open System
open System.Collections.Generic

type PacketPool (poolAmount) =

    let pool = Stack (Array.init poolAmount (fun _ -> new Packet ()))

    member this.Count = pool.Count

    member this.MaxCount = poolAmount

    member this.Get () = pool.Pop ()

    member this.Recycle (packet : Packet) =
        packet.Reset ()
        if pool.Count + 1 > poolAmount then
            failwith "For right now, this throws an exception" 
        pool.Push packet

[<AutoOpen>]
module private PacketPoolHelpers =

    let inline setFragmentId (fragmentId : byref<byte>) (packet : Packet) =
        packet.FragmentId <- fragmentId
        fragmentId <- fragmentId + 1uy

    let inline setFragment count (fragmentId : byref<byte>) packet =
        setFragmentId &fragmentId packet
        packet.FragmentCount <- count

    let inline createFragmentPacket count (fragmentId : byref<byte>) (packetPool : PacketPool) =
        let packet = packetPool.Get ()
        setFragment (byte count) &fragmentId packet
        packet

    let tryGetSequenceEntryId (packet : Packet) (seqId : byref<uint16>) : bool =
        if packet.FragmentId > 0uy then
            seqId <- packet.SequenceId - uint16 (packet.FragmentId - 1uy)
            true
        else
            false

    let fragment (bytes : byte []) (startIndex : int) (size : int) (packet : Packet) (packets : ResizeArray<Packet>) (packetPool : PacketPool) =
        let count = (size / int packet.DataLengthRemaining) + (if size % int packet.DataLengthRemaining > 0 then 1 else 0)

        if count > 255 then
            failwith "Fragmented count for data is greater than 255."

        let mutable startIndex = startIndex
        let mutable fragmentId = 1uy

        setFragment (byte count) &fragmentId packet

        packet.Write (bytes, startIndex, int packet.DataLengthRemaining)
        packets.Add packet

        for i = 1 to count - 1 do
            let packet = createFragmentPacket (byte count) &fragmentId packetPool

            if i = (count - 1) then
                let startIndex = startIndex + (i * int packet.DataLengthRemaining)
                packet.Write (bytes, startIndex, size - startIndex)
            else
                packet.Write (bytes, startIndex + (i * int packet.DataLengthRemaining), int packet.DataLengthRemaining)

            packets.Add packet 

    let getFromBytes bytes startIndex (size : int) (packets : ResizeArray<Packet>) (packetPool : PacketPool) =
        if packets.Count = 0 then
            let packet = packetPool.Get ()
            if int packet.DataLengthRemaining >= size then
                packet.Write (bytes, startIndex, size)
                packets.Add packet
            else
                fragment bytes startIndex size packet packets packetPool
                    
        else
            let packet = packets.[packets.Count - 1]
            if int packet.DataLengthRemaining >= size then
                packet.Write (bytes, startIndex, size)
            else
                let packet = packetPool.Get ()
                if int packet.DataLengthRemaining >= size then
                    packet.Write (bytes, startIndex, size)
                    packets.Add packet
                else
                    fragment bytes startIndex size packet packets packetPool

type PacketPool with

    member this.GetFromBytes (bytes, startIndex, size) =
        let packets = ResizeArray ()
        getFromBytes bytes startIndex size packets this
        packets

    member this.GetFromBytes (bytes, startIndex, size, outputPackets) =
        getFromBytes bytes startIndex size outputPackets this