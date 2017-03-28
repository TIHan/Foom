namespace Foom.Network

open System

[<Sealed>]
type AckManager =

    member ForEachPending : (int -> DateTime -> Packet -> unit) -> unit

    member Ack : int -> unit

    member Mark : Packet -> unit

