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

    | Merged = 7uy

[<Struct>]
type PacketHeader =
    { 
        packetType: PacketType
        sequenceId: uint16
        fragmentChunks: byte
        packetCount: byte
    }

    member x.PacketType = x.packetType

    member x.SequenceId = x.sequenceId

    member x.FragmentChunks = x.fragmentChunks

    member x.PacketCount = x.packetCount

[<Sealed>]
type Packet () =

    let byteStream = ByteStream (NetConstants.PacketSize)
    let byteWriter = ByteWriter (byteStream)
    let byteReader = ByteReader (byteStream)

    let mutable packetCount = 0
    let mutable packetType = PacketType.Unreliable

    let setPacketCount n =
        let originalPos = byteStream.Position
        byteStream.Position <- 5
        byteWriter.WriteByte (byte n)
        byteStream.Position <- originalPos

    member this.Length 
        with get () = byteStream.Length
        and set value = byteStream.Length <- value

    member this.Raw = byteStream.Raw

    member this.PacketType = LanguagePrimitives.EnumOfValue (byteStream.Raw.[0])

    member this.MergeCount = byteStream.Raw.[5] |> int

    member this.SetData (packetType, bytes: byte [], startIndex: int, size: int) =
        this.Reset ()

        // setup header
        byteWriter.Write { packetType = packetType; sequenceId = 0us; fragmentChunks = 0uy; packetCount = 0uy }
        byteWriter.WriteBytes (bytes, startIndex, size)

    member this.Merge (packetType, bytes, startIndex, size) =
        match this.PacketType with
        | PacketType.Merged ->

            packetCount <- packetCount + 1
            setPacketCount packetCount

            byteWriter.WriteBytes (bytes, startIndex, size)

        | _ -> failwith "Cannot merge data with a non-merged packet type."

    member this.Merge (packet: Packet) =
        this.Merge (packet.PacketType, packet.Raw, 0, packet.Length)

    member this.Reset () =
        byteStream.Length <- 0

    member this.Reader = byteReader
