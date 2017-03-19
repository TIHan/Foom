namespace Foom.Network

open System
open System.IO
open System.Collections.Generic

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

type ServerMessageType =
   | ConnectionEstablished = 0uy

   | UnreliableSequenced = 1uy

type ClientMessageType =
   | ConnectionRequested = 0uy

type ServerUnreliableChannel () =

    let writeStreams = Array.init 64 (fun _ -> new ByteStream (1024))
    let outgoingQueue = Queue<ByteStream> ()

    let mutable nextOutgoingSize = 0L
    let mutable seqN = 0us

    member this.EnqueueStream (stream: ByteStream) =
        outgoingQueue.Enqueue (stream)
        nextOutgoingSize <- nextOutgoingSize + stream.Length

    member this.Process (processStream: ByteStream -> unit) =
        ()
            
            
   

type IConnectedClient =

    abstract Address : string

type IServer =
    inherit IDisposable

    abstract Start : unit -> unit

    abstract Stop : unit -> unit

    abstract Heartbeat : unit -> unit

    abstract ClientConnected : IEvent<IConnectedClient>

    abstract Received : IEvent<IConnectedClient * BinaryReader>

type IClient =
    inherit IDisposable

    abstract Connect : string -> Async<bool>

    abstract Heartbeat : unit -> unit

    abstract Received : IEvent<BinaryReader>
