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
    { type'         : PacketType
      fragments     : byte
      sequenceId    : uint16
      size          : uint16
      fragmentId    : uint16
    }

[<Sealed>]
type Packet () =

    let byteStream = ByteStream (NetConstants.PacketSize + sizeof<PacketHeader>)
    let byteWriter = ByteWriter (byteStream)
    let byteReader = ByteReader (byteStream)

    do
        byteWriter.Write Unchecked.defaultof<PacketHeader>

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

    member this.FragmentId 
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

    member this.Fragments
        with get () =
            let originalPos = byteStream.Position
            byteStream.Position <- 1
            let value = byteReader.ReadByte ()
            byteStream.Position <- originalPos
            value

    member this.Size = this.Length - sizeof<PacketHeader>

    member this.LengthRemaining = byteStream.Raw.Length - byteStream.Length

    member this.WriteRawBytes (data, startIndex, size) =
        if byteStream.Position = 0 then
            byteWriter.Write Unchecked.defaultof<PacketHeader>

        byteWriter.WriteRawBytes (data, startIndex, size)

    member this.Reset () =
        byteStream.Length <- 0
        byteWriter.Write Unchecked.defaultof<PacketHeader>

    member this.Reader = byteReader

    member this.CopyTo (packet : Packet) =
        Buffer.BlockCopy (this.Raw, 0, packet.Raw, 0, this.Length)
        packet.Length <- this.Length
