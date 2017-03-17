namespace Foom.Network

open System

type IServer =

    abstract Start : unit -> bool

    abstract Heartbeat : unit -> unit


type IClient =

    abstract Connect : string -> Async<bool>
