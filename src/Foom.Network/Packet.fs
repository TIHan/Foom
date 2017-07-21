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
      fragmentId    : uint16
      sequenceId    : uint16
      pad0          : byte
      pad1          : byte
      pad2          : byte
    }

[<Sealed>]
type Packet () =

    let PacketSize = 1024

    let byteStream = ByteStream (PacketSize + sizeof<PacketHeader>)
    let byteWriter = ByteWriter (byteStream)
    let byteReader = ByteReader (byteStream)

    do
        byteWriter.Write Unchecked.defaultof<PacketHeader>

    member this.Length 
        with get () = byteStream.Length
        and set value = byteStream.Length <- value

    member this.DataLength = this.Length - sizeof<PacketHeader>

    member this.DataLengthRemaining = byteStream.Raw.Length - byteStream.Length

    member this.Raw = byteStream.Raw

    member this.Header =
        let originalPos = byteStream.Position
        byteStream.Position <- 0
        let value = byteReader.Read<PacketHeader> ()
        byteStream.Position <- originalPos
        value

    member this.Type
        with get () : PacketType = LanguagePrimitives.EnumOfValue (byteStream.Raw.[0])
        and set (value : PacketType) = byteStream.Raw.[0] <- byte value

    member this.SequenceId 
        with get () =
            let originalPos = byteStream.Position
            byteStream.Position <- 3
            let value = byteReader.ReadUInt16 ()
            byteStream.Position <- originalPos
            value

        and set value =
           let originalPos = byteStream.Position
           byteStream.Position <- 3
           byteWriter.WriteUInt16 value
           byteStream.Position <- originalPos

    member this.FragmentId 
        with get () =
            let originalPos = byteStream.Position
            byteStream.Position <- 1
            let value = byteReader.ReadUInt16 ()
            byteStream.Position <- originalPos
            value

        and set value =
           let originalPos = byteStream.Position
           byteStream.Position <- 1
           byteWriter.WriteUInt16 value
           byteStream.Position <- originalPos

    member this.WriteRawBytes (data, startIndex, size) =
        byteWriter.WriteRawBytes (data, startIndex, size)

    //member this.WriteInt (value) =
        //byteWriter.WriteInt value

    member this.Reset () =
        byteStream.Length <- 0
        byteWriter.Write Unchecked.defaultof<PacketHeader>

    member this.Reader = byteReader

    member this.CopyTo (packet : Packet) =
        Buffer.BlockCopy (this.Raw, 0, packet.Raw, 0, this.Length)
        packet.Length <- this.Length

    member this.ReadAcks f =
        let originalPos = byteStream.Position
        if this.Type = PacketType.ReliableOrderedAck then
            byteStream.Position <- sizeof<PacketHeader>
            while byteStream.Position < byteStream.Length do
                f (byteReader.ReadUInt16 ())

        byteStream.Position <- originalPos