namespace Foom.Network

open System

[<Sealed>]
type internal DataMerger =

    member Enqueue : struct (byte [] * int * int) -> unit

    member Flush : (Packet -> unit) -> unit

    member Reset : unit -> unit

    static member Create : PacketPool -> DataMerger