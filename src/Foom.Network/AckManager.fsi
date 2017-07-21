namespace Foom.Network

open System

[<Sealed>]
type AckManager =

    member Update : TimeSpan -> (uint16 -> Packet -> unit) -> unit

    member UpdateSequenced : TimeSpan -> (uint16 -> Packet -> unit) -> unit

    member ForEachPending : (uint16 -> Packet -> unit) -> unit

    member Ack : uint16 -> unit

    member MarkCopy : Packet * TimeSpan -> unit

    new : ackRetryTime : TimeSpan -> AckManager

