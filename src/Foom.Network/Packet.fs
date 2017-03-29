namespace Foom.Network

open System
open System.Collections.Generic

type PacketType =

    | Unreliable = 0uy
    | UnreliableSequenced = 1uy

    | Reliable = 2uy
    | ReliableAck = 3uy

    | ReliableSequenced = 4uy
    | ReliableSequencedAck = 5uy

    | ReliableOrdered = 6uy
    | ReliableOrderedAck = 7uy

    | ConnectionRequested = 8uy
    | ConnectionAccepted = 9uy



[<Struct>]
type PacketHeader =
    {
        type'       : PacketType
        fragments   : byte
        sequenceId  : uint16
        size        : uint16
    }

[<Sealed>]
type Packet () =

    let byteStream = ByteStream (NetConstants.PacketSize + sizeof<PacketHeader>)
    let byteWriter = ByteWriter (byteStream)
    let byteReader = ByteReader (byteStream)

    let setSize n =
        let originalPos = byteStream.Position
        byteStream.Position <- 4
        byteWriter.WriteUInt16 (uint16 n)
        byteStream.Position <- originalPos

    member this.Length 
        with get () = byteStream.Length
        and set value = byteStream.Length <- value

    member this.Raw = byteStream.Raw

    member this.PacketType
        with get () : PacketType = LanguagePrimitives.EnumOfValue (byteStream.Raw.[0])
        and set (value : PacketType) = byteStream.Raw.[0] <- byte value

    member this.SequenceId 
        with get () =
            let originalPos = byteStream.Position
            byteStream.Position <- 2
            let value = byteReader.ReadUInt16 ()
            byteStream.Position <- originalPos
            value

        and set value =
           let originalPos = byteStream.Position
           byteStream.Position <- 2
           byteWriter.WriteUInt16 value
           byteStream.Position <- originalPos

    member this.Size = this.Length - sizeof<PacketHeader>

    member this.LengthRemaining = byteStream.Raw.Length - byteStream.Length

    member this.SetData (data : byte [], startIndex : int, size : int) =
        this.Reset ()

        // setup header
        byteWriter.Write { type' = PacketType.Unreliable; sequenceId = 0us; fragments = 0uy; size = uint16 size }
        byteWriter.WriteRawBytes (data, startIndex, size)

        byteStream.Position <- 0

    member this.Set (data, startIndex, size) =
        this.Reset ()

        byteWriter.WriteRawBytes (data, startIndex, size)
        byteStream.Position <- 0

    member this.Merge (packet : Packet) =
        let originalPos = byteStream.Position
        byteStream.Position <- sizeof<PacketHeader>
        byteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.Size)
        byteStream.Position <- originalPos
        setSize this.Size

    member this.Combine (packet : Packet) =
        let originalPos = byteStream.Position
        byteWriter.WriteRawBytes (packet.Raw, 0, packet.Length)
        byteStream.Position <- originalPos

    member this.Reset () =
        byteStream.Length <- 0

    member this.Reader = byteReader

    member this.CopyTo (packet : Packet) =
        Buffer.BlockCopy (this.Raw, 0, packet.Raw, 0, this.Length)
        packet.Length <- this.Length
