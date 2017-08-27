namespace Foom.Network

open System

[<Sealed>]
type BasicChannelState =

    member UpdateReceive : TimeSpan -> unit

    member UpdateSend : TimeSpan -> unit

module BasicChannelState =

    val create : PacketPool -> (Packet -> unit) -> (Packet -> unit) -> (uint16 -> (byte [] * int * int -> unit) -> unit) -> BasicChannelState

    val send : byte [] -> int -> int -> PacketType -> BasicChannelState -> unit

    val receive : TimeSpan -> Packet -> BasicChannelState -> bool