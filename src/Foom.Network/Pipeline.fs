namespace Foom.Network

open System
open System.Collections.Generic

type PipelineContext<'T> () =

    member val Send : ('T -> unit) option = None with get, set

    member val OutputEvent = None with get, set

    member val SubscribeActions = ResizeArray<unit -> unit> ()

    member val ProcessActions = ResizeArray<TimeSpan -> unit> ()

    member val CleanupActions = ResizeArray<unit -> unit> ()

type PipelineBuilder<'Input, 'Output> = PipelineBuilder of (PipelineContext<'Input> -> unit)

[<Sealed>]
type Pipeline<'Input, 'Output> (evt : Event<'Output>, send : 'Input -> unit, process' : TimeSpan -> unit) =

    member this.Send input =
        send input

    member this.Process time =
        process' time

    member this.Output = evt.Publish

module Pipeline =

    let create () : PipelineBuilder<'Input, 'Input> =
        PipelineBuilder (fun context ->
            let evt = Event<'Input> ()
            context.Send <- evt.Trigger |> Some
            context.OutputEvent <- (evt :> obj) |> Some
        )

    let build (pipelineBuilder : PipelineBuilder<'Input, 'Output>) =
        let context = PipelineContext ()
        match pipelineBuilder with
        | PipelineBuilder f -> f context

        context.SubscribeActions
        |> Seq.iter (fun f -> f ())

        Pipeline (context.OutputEvent.Value :?> Event<'Output>, context.Send.Value, fun time -> 
            context.ProcessActions
            |> Seq.iter (fun f ->
                f time
            )

            context.CleanupActions
            |> Seq.iter (fun f ->
                f ()
            )
        )

    let filter (filter : (TimeSpan -> 'Output seq -> ('NewOutput -> unit) -> unit)) (pipeline : PipelineBuilder<'Input, 'Output>) : PipelineBuilder<'Input, 'NewOutput> =
        PipelineBuilder (fun context ->
            match pipeline with
            | PipelineBuilder f -> f context

            let evt = context.OutputEvent.Value :?> Event<'Output>

            let inputs = ResizeArray ()
            context.SubscribeActions.Add(fun () ->
                evt.Publish.Add inputs.Add
            )

            let evt = Event<'NewOutput> ()
            context.ProcessActions.Add(fun time ->
                filter time inputs evt.Trigger
            )
            context.OutputEvent <- (evt :> obj) |> Some

            context.CleanupActions.Add(fun () ->
                inputs.Clear ()
            )
        )

    let sink f (pipeline : PipelineBuilder<'Input, 'Output>) : Pipeline<'Input, 'Output> =
        PipelineBuilder (fun context ->
            match pipeline with
            | PipelineBuilder f -> f context

            let evt = context.OutputEvent.Value :?> Event<'Output>
            evt.Publish.Add f
        )
        |> build

    let map f pipeline =
        pipeline
        |> filter (fun _ xs callback ->
            xs |> Seq.iter (fun x -> callback (f x))
        )
