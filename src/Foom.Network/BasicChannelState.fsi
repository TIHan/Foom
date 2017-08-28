namespace Foom.Network

open System

[<Sealed>]
type BasicChannelState =

    static member Create : PacketPool * (Packet -> unit) * (Packet -> unit) * (uint16 -> (byte [] * int * int -> unit) -> unit) -> BasicChannelState

    member Receive : TimeSpan * Packet -> bool

    member Send : byte[] * int * int * PacketType -> unit

    member UpdateReceive : TimeSpan -> unit

    member UpdateSend : TimeSpan -> unit