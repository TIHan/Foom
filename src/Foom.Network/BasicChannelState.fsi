namespace Foom.Network

open System

[<Sealed>]
type BasicChannelState =

    static member Create : PacketPool * (Packet -> unit) * (Packet -> unit) -> BasicChannelState

    member Receive : Packet -> bool

    member SendUnreliable : byte[] * int * int -> unit

    member SendReliableOrdered : byte [] * int * int -> unit

    member SendReliableOrderedAck : uint16 -> unit

    member Update : TimeSpan -> unit
