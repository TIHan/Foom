namespace Foom.Network

[<Sealed>]
type internal DataMerger =

    member Enqueue : byte [] * startIndex : int * size : int -> unit

    member Flush : PacketPool * ResizeArray<Packet> -> unit

    static member Create : unit -> DataMerger