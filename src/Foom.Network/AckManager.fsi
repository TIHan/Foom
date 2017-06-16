namespace Foom.Network

open System

[<Sealed>]
type AckManager =

    member Update : TimeSpan -> (int -> Packet -> unit) -> unit

    member UpdateSequenced : TimeSpan -> (int -> Packet -> unit) -> unit

    member ForEachPending : (int -> Packet -> unit) -> unit

    member Ack : int -> unit

    member MarkCopy : Packet * TimeSpan -> unit

    new : ackRetryTime : TimeSpan -> AckManager

