namespace Foom.Network

open System

[<Sealed>]
type AckManager =

    member Update : TimeSpan -> (uint16 -> Packet -> unit) -> unit

    member ForEachPending : (uint16 -> Packet -> unit) -> unit

    member Ack : uint16 -> unit

    member Mark : Packet * TimeSpan -> unit

    member GetPacket : int -> Packet

    new : ackRetryTime : TimeSpan -> AckManager

