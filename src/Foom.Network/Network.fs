namespace Foom.Network

open System
open System.IO
open System.Collections.Generic

type IServer =
    inherit IDisposable

    abstract Start : port: int -> bool

    abstract Stop : unit -> unit

    abstract Update : unit -> unit

    abstract ClientConnected : IEvent<unit>

    abstract ClientDisconnected : IEvent<unit>

type IClient =
    inherit IDisposable

    abstract Connect : address: string * port: int -> unit

    abstract Disconnect : unit -> unit

    abstract Update : unit -> unit

    abstract Connected : IEvent<unit>

    abstract Disconnected : IEvent<unit>
