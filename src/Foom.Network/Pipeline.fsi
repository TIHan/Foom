namespace Foom.Network

open System

type ISource =

    abstract Send : byte [] * startIndex: int * size: int * (Packet -> unit) -> unit

type IFilter =

    abstract Send : Packet -> unit

    abstract Process : (Packet -> unit) -> unit

[<Sealed; NoEquality; NoComparison>]
type Pipeline =

    member Send : byte [] * startIndex: int * size: int -> unit

    member Process : unit -> unit

[<Sealed; NoEquality; NoComparison>]
type Element

module Pipeline =

    val create : ISource -> Element

    val add : IFilter -> Element -> Element

    val sink : (Packet -> unit) -> Element -> Pipeline