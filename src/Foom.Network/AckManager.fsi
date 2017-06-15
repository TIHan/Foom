namespace Foom.Network

open System

[<Sealed>]
type AckManager =

    member ForEachPending : TimeSpan -> (int -> Packet -> unit) -> unit

    member Ack : int -> unit

    member MarkCopy : Packet * TimeSpan -> unit

    new : ackRetryTime : TimeSpan -> AckManager

