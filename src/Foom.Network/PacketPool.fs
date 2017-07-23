namespace Foom.Network

open System
open System.Collections.Generic

type PacketPool (poolAmount) =

    let pool = Stack (Array.init poolAmount (fun _ -> Packet ()))

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
        let count = (size / packet.DataLengthRemaining) + (if size % packet.DataLengthRemaining > 0 then 1 else 0)

        if count > 255 then
            failwith "Fragmented count for data is greater than 255."

        let mutable startIndex = startIndex
        let mutable fragmentId = 1uy

        setFragment (byte count) &fragmentId packet

        packet.WriteRawBytes (bytes, startIndex, packet.DataLengthRemaining)
        packets.Add packet

        for i = 1 to count - 1 do
            let packet = createFragmentPacket (byte count) &fragmentId packetPool

            if i = (count - 1) then
                let startIndex = startIndex + (i * packet.DataLengthRemaining)
                packet.WriteRawBytes (bytes, startIndex, size - startIndex)
            else
                packet.WriteRawBytes (bytes, startIndex + (i * packet.DataLengthRemaining), packet.DataLengthRemaining)

            packets.Add packet 

    let getFromBytes bytes startIndex size (packets : ResizeArray<Packet>) (packetPool : PacketPool) =
        if packets.Count = 0 then
            let packet = packetPool.Get ()
            if packet.DataLengthRemaining >= size then
                packet.WriteRawBytes (bytes, startIndex, size)
                packets.Add packet
            else
                fragment bytes startIndex size packet packets packetPool
                    
        else
            let packet = packets.[packets.Count - 1]
            if packet.DataLengthRemaining >= size then
                packet.WriteRawBytes (bytes, startIndex, size)
            else
                let packet = packetPool.Get ()
                if packet.DataLengthRemaining >= size then
                    packet.WriteRawBytes (bytes, startIndex, size)
                    packets.Add packet
                else
                    fragment bytes startIndex size packet packets packetPool

type FragmentBuffer =
    {
        packets : Packet []
        mutable count : int
    }

    static member Create () =
        {
            packets = Array.zeroCreate 256
            count = 0
        }
    
    member this.Reset () =
        System.Array.Clear (this.packets, 0, this.packets.Length)
        this.count <- 0

type FragmentBufferPool (poolAmount) =

    let pool = Stack (Array.init poolAmount (fun _ -> FragmentBuffer.Create ()))

    member this.Count = pool.Count

    member this.Get () = pool.Pop ()

    member this.Recycle (packet : FragmentBuffer) =
        packet.Reset ()
        if pool.Count + 1 > poolAmount then
            failwith "For right now, this throws an exception" 
        pool.Push packet

type FragmentAssembler () =

    let fragmentBufferPool = FragmentBufferPool (256)
    let fragmentBuffers = Array.init 65536 (fun _ -> Unchecked.defaultof<FragmentBuffer>)

    member this.Mark (packet : Packet, f) =

        let mutable seqId = 0us
        if tryGetSequenceEntryId packet &seqId then

            if obj.ReferenceEquals (fragmentBuffers.[int seqId], null) |> not then

                let fragmentBuffer = fragmentBuffers.[int seqId]
                fragmentBuffer.packets.[int packet.FragmentId] <- packet
                fragmentBuffer.count <- fragmentBuffer.count + 1

                if int packet.FragmentCount = fragmentBuffer.count then
                    for i = 1 to fragmentBuffer.count do
                        f (fragmentBuffer.packets.[i])
                    fragmentBufferPool.Recycle fragmentBuffer
                    fragmentBuffers.[int seqId] <- Unchecked.defaultof<FragmentBuffer>

            else
                let fragmentBuffer = fragmentBufferPool.Get ()
                fragmentBuffers.[int seqId] <- fragmentBuffer

                fragmentBuffer.packets.[int packet.FragmentId] <- packet
                fragmentBuffer.count <- fragmentBuffer.count + 1

        else
            failwith "bad packet"



type PacketPool with

    member this.GetFromBytes (bytes, startIndex, size) =
        let packets = ResizeArray ()
        getFromBytes bytes startIndex size packets this
        packets

    member this.GetFromBytes (bytes, startIndex, size, outputPackets) =
        getFromBytes bytes startIndex size outputPackets this