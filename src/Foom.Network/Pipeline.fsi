namespace Foom.Network

open System

type ISource =

    abstract Send : byte [] * startIndex: int * size: int -> unit

    abstract Listen : IObservable<Packet>

type IFilter =

    abstract Send : Packet -> unit

    abstract Listen : IObservable<Packet>

    abstract Process : unit -> unit

[<Sealed; NoEquality; NoComparison>]
type Pipeline =

    member Send : byte [] * startIndex: int * size: int -> unit

    member Process : unit -> unit

[<Sealed; NoEquality; NoComparison>]
type Element

module Pipeline =

    val create : ISource -> Element

    val filter : IFilter -> Element -> Element

    val sink : (Packet -> unit) -> Element -> Pipeline