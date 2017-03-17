namespace Foom.Network

open System

type IServer =
    inherit IDisposable

    abstract Start : unit -> unit

    abstract Stop : unit -> unit

    abstract Heartbeat : unit -> unit

    abstract ClientConnected : IEvent<string>

type IClient =
    inherit IDisposable

    abstract Connect : string -> Async<bool>
