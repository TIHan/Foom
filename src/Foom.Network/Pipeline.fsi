namespace Foom.Network

open System

module NewPipeline =

    type Filter<'Input, 'Output> = Filter of ('Input seq -> ('Output -> unit) -> unit)

    [<Sealed>]
    type PipelineBuilder<'Input, 'Output>

    [<Sealed>]
    type Pipeline<'Input, 'Output> =

        member Send : 'Input -> unit

        member Process : unit -> unit

        member Output : IEvent<'Output>

    val createPipeline : Filter<'Input, 'Output> -> PipelineBuilder<'Input, 'Output>

    val addFilter : Filter<'Output, 'NewOutput> -> PipelineBuilder<'Input, 'Output> -> PipelineBuilder<'Input, 'NewOutput>

    val sink : ('Output -> unit) -> (PipelineBuilder<'Input, 'Output>) -> PipelineBuilder<'Input, 'Output>

    val build : PipelineBuilder<'Input, 'Output> -> Pipeline<'Input, 'Output>

    val mapFilter : ('a -> 'b) -> Filter<'a, 'b>

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