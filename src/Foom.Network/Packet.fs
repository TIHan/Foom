namespace Foom.Network

open System
open System.Collections.Generic

(*
- Unreliable -
    - Send              |-> DataMerger |> MainQueue
    - Receive           |-> Passthrough |> MainQueue

UnreliableSequenced         |-> DataMerger |> Sequencer |> LastestAck

Reliable |->                DataMerger |>
*)

type PacketType =

    | Unreliable = 0uy
    | UnreliableSequenced = 1uy

    | Reliable = 2uy
    | ReliableAck = 3uy

    | ReliableSequenced = 4uy
    | ReliableSequencedAck = 5uy

    | ReliableOrdered = 6uy
    | ReliableOrderedAck = 7uy

    | Snapshot = 8uy
    | SnapshotAck = 9uy

    | ConnectionRequested = 10uy
    | ConnectionAccepted = 11uy

    | Ping = 12uy
    | Pong = 13uy

    | Disconnect = 14uy

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
        with get () = LitteEndian.read8 this.Raw 3
        and set (value : byte) = LitteEndian.write8 this.Raw 3 value

    member this.FragmentCount
        with get () = LitteEndian.read8 this.Raw 4
        and set (value : byte) = LitteEndian.write8 this.Raw 4 value

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


