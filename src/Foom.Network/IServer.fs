namespace Foom.Network

open System
open System.IO

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

    abstract BroadcastReliableString : string -> unit

    abstract DebugBroadcastReliableString : string * uint16 -> unit

type IClient =
    inherit IDisposable

    abstract Connect : string -> Async<bool>

    abstract Heartbeat : unit -> unit

    abstract Received : IEvent<BinaryReader>
