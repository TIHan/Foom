namespace Foom.Network

open System

[<Sealed>]
type PipelineBuilder<'Input, 'Output>

[<Sealed>]
type Pipeline<'Input> =

    member Send : 'Input -> unit

    member Process : TimeSpan -> unit

module Pipeline =

    val create : unit -> PipelineBuilder<'Input, 'Input>

    val filter : (TimeSpan -> 'Output seq -> ('NewOutput -> unit) -> unit) -> PipelineBuilder<'Input, 'Output> -> PipelineBuilder<'Input, 'NewOutput>

    val sink : ('Output -> unit) -> (PipelineBuilder<'Input, 'Output>) -> Pipeline<'Input>

    val map : ('b -> 'c) -> PipelineBuilder<'a, 'b> -> PipelineBuilder<'a, 'c>

    val merge2 : (('Input -> unit) -> ('Input -> unit) -> 'Input -> unit) -> PipelineBuilder<'Input, 'Output> -> PipelineBuilder<'Input, 'Output> -> PipelineBuilder<'Input, 'Output>
