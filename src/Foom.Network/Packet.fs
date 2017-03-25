﻿namespace Foom.Network

open System
open System.Collections.Generic

type PacketType =

    | Unreliable = 0uy
    | UnreliableSequenced = 1uy
    | Reliable = 2uy
    | ReliableSequenced = 3uy
    | ReliableOrdered = 4uy

    | ConnectionRequested = 5uy
    | ConnectionAccepted = 6uy

    | Disconnected = 7uy
    | Kicked = 8uy
    | Banned = 9uy

[<Struct>]
type PacketHeader =
    {
        type'       : PacketType
        fragments   : byte
        size        : uint16
        sequenceId  : uint16
    }

[<Sealed>]
type Packet () =

    let byteStream = ByteStream (NetConstants.PacketSize)
    let byteWriter = ByteWriter (byteStream)
    let byteReader = ByteReader (byteStream)

    member this.Length 
        with get () = byteStream.Length
        and set value = byteStream.Length <- value

    member this.Raw = byteStream.Raw

    member this.PacketType : PacketType = LanguagePrimitives.EnumOfValue (byteStream.Raw.[0])

    member this.SequenceId 
        with get () =
            let originalPos = byteStream.Position
            byteStream.Position <- 4
            let value = byteReader.ReadUInt16 ()
            byteStream.Position <- originalPos
            value

        and set value =
           let originalPos = byteStream.Position
           byteStream.Position <- 4
           byteWriter.WriteUInt16 value
           byteStream.Position <- originalPos

    member this.LengthRemaining = byteStream.Raw.Length - byteStream.Length

    member this.SetData (packetType, bytes: byte [], startIndex: int, size: int) =
        this.Reset ()

        // setup header
        byteWriter.Write { type' = packetType; sequenceId = 0us; fragments = 0uy; size = uint16 size }
        byteWriter.WriteRawBytes (bytes, startIndex, size)

    member this.Merge (packet : Packet) =
        byteWriter.WriteRawBytes (packet.Raw, 0, packet.Length)

    member this.Reset () =
        byteStream.Length <- 0

    member this.Reader = byteReader

    member this.CopyTo (packet : Packet) =
        Buffer.BlockCopy (this.Raw, 0, packet.Raw, 0, this.Length)
        packet.Length <- this.Length
