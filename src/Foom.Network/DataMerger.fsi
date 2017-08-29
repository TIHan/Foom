namespace Foom.Network

[<Sealed>]
type internal DataMerger =

    member Enqueue : byte [] * startIndex : int * size : int -> unit

    member Flush : (Packet -> unit) -> unit

    static member Create : PacketPool -> DataMerger