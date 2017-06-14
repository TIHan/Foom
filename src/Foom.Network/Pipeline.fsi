namespace Foom.Network

open System

[<Sealed>]
type PipelineBuilder<'Input, 'Output>

[<Sealed>]
type Pipeline<'Input, 'Output> =

    member Send : 'Input -> unit

    member Process : unit -> unit

    member Output : IEvent<'Output>

module Pipeline =

    val create : unit -> PipelineBuilder<'Input, 'Input>

    val demux : (('b -> unit) -> ('c -> unit) -> 'a -> unit) -> PipelineBuilder<'Input, 'a> -> PipelineBuilder<'Input, ('b seq * 'c seq)>

    val filter : ('Output seq -> ('NewOutput -> unit) -> unit) -> PipelineBuilder<'Input, 'Output> -> PipelineBuilder<'Input, 'NewOutput>

    val sink : ('Output -> unit) -> (PipelineBuilder<'Input, 'Output>) -> PipelineBuilder<'Input, 'Output>

    val build : PipelineBuilder<'Input, 'Output> -> Pipeline<'Input, 'Output>

    val map : ('b -> 'c) -> PipelineBuilder<'a, 'b> -> PipelineBuilder<'a, 'c>
