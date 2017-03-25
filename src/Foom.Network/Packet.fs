namespace Foom.Network

open System
open System.Collections.Generic

type PacketType =

    | Unreliable = 0uy
    | UnreliableSequenced = 1uy

    | ConnectionRequested = 2uy
    | ConnectionAccepted = 3uy

    | Disconnected = 4uy
    | Kicked = 5uy
    | Banned = 6uy

[<Struct>]
type PacketHeader =
    {
        packetType: PacketType
        sequenceId: uint16
        fragmentChunks: byte
        mergeCount: byte
    }

[<Sealed>]
type Packet () =

    let byteStream = ByteStream (NetConstants.PacketSize)
    let byteWriter = ByteWriter (byteStream)
    let byteReader = ByteReader (byteStream)

    let mutable mergeCount = 0
    let mutable packetType = PacketType.Unreliable

    let setMergeCount n =
        let originalPos = byteStream.Position
        byteStream.Position <- 5
        byteWriter.WriteByte (byte n)
        byteStream.Position <- originalPos

    member this.Length 
        with get () = byteStream.Length
        and set value = byteStream.Length <- value

    member this.Raw = byteStream.Raw

    member this.PacketType : PacketType = LanguagePrimitives.EnumOfValue (byteStream.Raw.[0])

    member this.MergeCount = byteStream.Raw.[5] |> int

    member this.SetData (packetType, bytes: byte [], startIndex: int, size: int) =
        this.Reset ()

        // setup header
        byteWriter.Write { packetType = packetType; sequenceId = 0us; fragmentChunks = 0uy; mergeCount = 0uy }
        byteWriter.WriteRawBytes (bytes, startIndex, size)

    member this.Merge (packet : Packet) =
        mergeCount <- mergeCount + 1
        setMergeCount mergeCount

        byteWriter.WriteRawBytes (packet.Raw, 0, packet.Length)

    member this.Reset () =
        byteStream.Length <- 0

    member this.Reader = byteReader
