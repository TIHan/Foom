namespace Foom.Network

open System

type Filter<'Input, 'Output> = Filter of ('Input seq -> ('Output -> unit) -> unit)

[<Sealed>]
type PipelineBuilder<'Input, 'Output>

[<Sealed>]
type Pipeline<'Input, 'Output> =

    member Send : 'Input -> unit

    member Process : unit -> unit

    member Output : IEvent<'Output>

module Pipeline =

    val create : Filter<'Input, 'Output> -> PipelineBuilder<'Input, 'Output>

    val addFilter : Filter<'Output, 'NewOutput> -> PipelineBuilder<'Input, 'Output> -> PipelineBuilder<'Input, 'NewOutput>

    val sink : ('Output -> unit) -> (PipelineBuilder<'Input, 'Output>) -> PipelineBuilder<'Input, 'Output>

    val build : PipelineBuilder<'Input, 'Output> -> Pipeline<'Input, 'Output>

    val mapFilter : ('a -> 'b) -> Filter<'a, 'b>
