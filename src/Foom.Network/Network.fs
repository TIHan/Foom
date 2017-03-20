namespace Foom.Network

open System
open System.IO
open System.Collections.Generic

type IWriter =

    abstract Put : int32 -> unit

type IReader =

    abstract GetInt : unit -> int32

type IServer =
    inherit IDisposable

    abstract Start : port: int -> bool

    abstract Stop : unit -> unit

    abstract Update : unit -> unit

    abstract ClientConnected : IEvent<unit>

    abstract ClientDisconnected : IEvent<unit>

    abstract SendToAll<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> : 'T -> unit

    abstract RegisterType<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> : (IWriter -> 'T -> unit) * (IReader -> 'T) -> unit

    abstract Subscribe<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> : ('T -> unit) -> unit

type IClient =
    inherit IDisposable

    abstract Connect : address: string * port: int -> unit

    abstract Disconnect : unit -> unit

    abstract Update : unit -> unit

    abstract Connected : IEvent<unit>

    abstract Disconnected : IEvent<unit>

    abstract RegisterType<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> : (IWriter -> 'T -> unit) * (IReader -> 'T) -> unit

    abstract Subscribe<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> : ('T -> unit) -> unit
