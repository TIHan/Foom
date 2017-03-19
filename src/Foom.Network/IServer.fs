namespace Foom.Network

open System
open System.IO

type ByteStream (size) =

    let buffer = Array.zeroCreate<byte> size
    let ms = new MemoryStream (buffer)
    let reader = new BinaryReader (ms)
    let writer = new BinaryWriter (ms)

    member this.Buffer = buffer

    member this.Reader = reader

    member this.Writer = writer

    member this.Position
        with get () = ms.Position
        and set position = ms.Position <- position

    member this.SetLength value =
        ms.SetLength (value)

    member this.Length = ms.Length

    interface IDisposable with
        
        member this.Dispose () =
            reader.Dispose ()
            writer.Dispose ()
            ms.Dispose ()


type OutgoingMessage () =

    member val Stream = new ByteStream (1024)

    member this.Writer = this.Stream.Writer
    


type ServerMessageType =
    | ConnectionEstablished = 0uy
    | ReliableOrder = 1uy

type ClientMessageType =
    | ConnectionRequested = 0uy
    | AckReliableOrder = 1uy

type IConnectedClient =

    abstract Address : string

type IServer =
    inherit IDisposable

    abstract Start : unit -> unit

    abstract Stop : unit -> unit

    abstract Heartbeat : unit -> unit

    abstract ClientConnected : IEvent<IConnectedClient>

    abstract Received : IEvent<IConnectedClient * BinaryReader>

    abstract CreateMessage : unit -> OutgoingMessage

    abstract SendMessage : OutgoingMessage -> unit

type IClient =
    inherit IDisposable

    abstract Connect : string -> Async<bool>

    abstract Heartbeat : unit -> unit

    abstract Received : IEvent<BinaryReader>
