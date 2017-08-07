namespace Foom.Network

open System
open System.Collections.Generic

type INetworkEncryption =
    inherit IDisposable

    abstract Encrypt : bytes : byte [] * offset : int * count : int * output : byte [] * outputOffset : int * outputMaxCount : int -> int

    abstract Decrypt : bytes : byte [] * offset : int * count : int * output : byte [] * outputOffset : int * outputMaxCount : int -> int

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

    | Ping = 10uy
    | Pong = 11uy

    | Disconnect = 12uy

[<Struct>]
type PacketHeader =
    { type'         : PacketType // 1 byte
      sequenceId    : uint16 // 2 bytes
      fragmentId    : byte
      fragmentCount : byte
    }

[<Sealed>]
type Packet () as this =
    inherit ByteStream (Array.zeroCreate <| 1024 + sizeof<PacketHeader>)

    do
        this.Writer.Write Unchecked.defaultof<PacketHeader>

    member this.DataLength = this.Length - int64 sizeof<PacketHeader>

    member this.DataLengthRemaining = int64 this.Raw.Length - this.Length

    member this.Header =
        let originalPos = this.Position
        this.Position <- 0L
        let value = this.Reader.Read<PacketHeader> ()
        this.Position <- originalPos
        value

    member this.Type
        with get () : PacketType = LanguagePrimitives.EnumOfValue (this.Raw.[0])
        and set (value : PacketType) = this.Raw.[0] <- byte value

    member this.SequenceId 
        with get () = LitteEndian.read16 this.Raw 1
        and set (value : uint16) = LitteEndian.write16 this.Raw 1 value

    member this.FragmentId 
        with get () = LitteEndian.read8 this.Raw 3 0
        and set (value : byte) = LitteEndian.write8 this.Raw 3 0 value

    member this.FragmentCount
        with get () = LitteEndian.read8 this.Raw 4 0
        and set (value : byte) = LitteEndian.write8 this.Raw 4 0 value

    member this.Reset () =
        this.SetLength 0L
        this.Writer.Write Unchecked.defaultof<PacketHeader>

    member this.CopyTo (packet : Packet) =
        packet.SetLength this.Length
        Buffer.BlockCopy (this.Raw, 0, packet.Raw, 0, int this.Length)

    member this.ReadAcks f =
        let originalPos = this.Position
        if this.Type = PacketType.ReliableOrderedAck then
            this.Position <- int64 sizeof<PacketHeader>
            while this.Position < this.Length do
                f (this.Reader.ReadUInt16 ())

        this.Position <- originalPos

    member this.IsFragmented = this.FragmentId > 0uy
