namespace Foom.Network

open System
open System.Collections.Generic

type Filter<'Input, 'Output> = Filter of ('Input seq -> ('Output -> unit) -> unit)

type PipelineContext<'T> () =

    member val Send : ('T -> unit) option = None with get, set

    member val OutputEvent = None with get, set

    member val SubscribeActions = ResizeArray<unit -> unit> ()

    member val ProcessActions = ResizeArray<unit -> unit> ()

type PipelineBuilder<'Input, 'Output> = PipelineBuilder of (PipelineContext<'Input> -> unit)

[<Sealed>]
type Pipeline<'Input, 'Output> (evt : Event<'Output>, send : 'Input -> unit, process' : unit -> unit) =

    member this.Send input =
        send input

    member this.Process () =
        process' ()

    member this.Output = evt.Publish

module Pipeline =

    let create () : PipelineBuilder<'Input, 'Input> =
        PipelineBuilder (fun context ->
            let evt = Event<'Input> ()
            context.Send <- evt.Trigger |> Some
            context.OutputEvent <- (evt :> obj) |> Some
        )

    let demux (f :  ('a -> ('b -> unit) -> ('c -> unit) -> unit)) (pipeline : PipelineBuilder<'Input, 'a>) : PipelineBuilder<'Input, ('b seq * 'c seq)> =
        PipelineBuilder (fun context ->
            match pipeline with
            | PipelineBuilder f -> f context

            let evt = context.OutputEvent.Value :?> Event<'a>

            let inputs = ResizeArray<'b> ()
            let inputs2 = ResizeArray<'c> ()
            context.SubscribeActions.Add(fun () ->
                evt.Publish.Add (fun x -> f x inputs.Add inputs2.Add)
            )

            let evt = Event<_> ()
            context.ProcessActions.Add(fun () ->
                evt.Trigger (inputs :> 'b seq, inputs2 :> 'c seq)
                inputs.Clear ()
                inputs2.Clear ()
            )
            context.OutputEvent <- (evt :> obj) |> Some
        )

    let addFilter (filter : Filter<'Output, 'NewOutput>) (pipeline : PipelineBuilder<'Input, 'Output>) : PipelineBuilder<'Input, 'NewOutput> =
        PipelineBuilder (fun context ->
            match pipeline with
            | PipelineBuilder f -> f context

            let processFilter =
                match filter with
                | Filter x -> x

            let evt = context.OutputEvent.Value :?> Event<'Output>

            let inputs = ResizeArray ()
            context.SubscribeActions.Add(fun () ->
                evt.Publish.Add inputs.Add
            )

            let evt = Event<'NewOutput> ()
            context.ProcessActions.Add(fun () ->
                processFilter inputs evt.Trigger
                inputs.Clear ()
            )
            context.OutputEvent <- (evt :> obj) |> Some
        )

    let sink f (pipeline : PipelineBuilder<'Input, 'Output>) : PipelineBuilder<'Input, 'Output> =
        PipelineBuilder (fun context ->
            match pipeline with
            | PipelineBuilder f -> f context

            let evt = context.OutputEvent.Value :?> Event<'Output>
            evt.Publish.Add f
        )

    let build (pipelineBuilder : PipelineBuilder<'Input, 'Output>) =
        let context = PipelineContext ()
        match pipelineBuilder with
        | PipelineBuilder f -> f context

        context.SubscribeActions
        |> Seq.iter (fun f -> f ())

        Pipeline (context.OutputEvent.Value :?> Event<'Output>, context.Send.Value, fun () -> 
            context.ProcessActions
            |> Seq.iter (fun f ->
                f ()
            )
        )

    let mapFilter f =
        Filter (fun xs callback ->
            xs |> Seq.iter (fun x -> callback (f x))
        )
