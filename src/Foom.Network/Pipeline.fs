namespace Foom.Network

open System
open System.Collections.Generic

module NewPipeline =

    [<Sealed>]
    type Filter<'Input, 'Output> (f : 'Input -> ResizeArray<'Output> -> unit) =

        let outputs = ResizeArray ()

        member this.Send (input : 'Input) =
            f input outputs

        member this.Process (f : 'Output -> unit) =
            for i = 0 to outputs.Count - 1 do
                let output = outputs.[i]
                f output

            outputs.Clear ()

    let PacketMerger (packetPool : PacketPool) =
        Filter (fun (packet : Packet) (packets : ResizeArray<Packet>) ->
            if packets.Count > 0 then

                let mutable done' = false
                for i = 0 to packets.Count - 1 do
                    let packet' = packets.[i]
                    if packet'.LengthRemaining > packet.Length && not done' then
                        packet'.Merge packet
                        done' <- true

                if not done' then
                    let packet' = packetPool.Get ()
                    packet.CopyTo packet'
                    packets.Add packet'
            else
                let packet' = packetPool.Get ()
                packet.CopyTo packet'
                packets.Add packet'

            packetPool.Recycle packet
        )

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

    let createPipeline (filter : Filter<'Input, 'Output>) : PipelineBuilder<'Input, 'Output> =
        PipelineBuilder (fun context ->
            context.Send <- filter.Send |> Some

            let evt = Event<'Output> ()
            context.ProcessActions.Add(fun () ->
                filter.Process (evt.Trigger)
            )
            context.OutputEvent <- (evt :> obj) |> Some
        )

    let addFilter (filter : Filter<'Output, 'NewOutput>) (pipeline : PipelineBuilder<'Input, 'Output>) : PipelineBuilder<'Input, 'NewOutput> =
        PipelineBuilder (fun context ->
            match pipeline with
            | PipelineBuilder f -> f context

            let evt = context.OutputEvent.Value :?> Event<'Output>

            context.SubscribeActions.Add(fun () ->
                evt.Publish.Add (filter.Send)
            )

            let evt = Event<'NewOutput> ()
            context.ProcessActions.Add(fun () ->
                filter.Process (evt.Trigger)
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

type ISource =

    abstract Send : byte [] * startIndex: int * size: int * (Packet -> unit) -> unit

type IFilter =

    abstract Send : Packet -> unit

    abstract Process : (Packet -> unit) -> unit

type Pipeline =
    {
        [<DefaultValue>] mutable source: byte [] -> int -> int -> unit
        actions : ResizeArray<unit -> unit>
        mutable nextListener : IObservable<Packet>
    }

    member this.Send (data, startIndex, size) =
        if obj.ReferenceEquals (this.source, null) |> not then
            this.source data startIndex size
     
    member this.Process () =
        for i = 0 to this.actions.Count - 1 do
            let action = this.actions.[i]
            action ()

type Element = Element of (Pipeline -> unit)

module Pipeline =

    let create (src : ISource) =
        Element (fun context ->
            let outputEvent = Event<Packet> ()
            context.source <- fun data startIndex size -> src.Send (data, startIndex, size, outputEvent.Trigger)
            context.nextListener <- outputEvent.Publish
        )

    let add (filter : IFilter) el =
        Element (fun context ->
            match el with
            | Element f -> f context

            let outputEvent = Event<Packet> ()
            if context.nextListener <> null then
                context.nextListener.Add filter.Send
                context.actions.Add (fun () -> filter.Process outputEvent.Trigger)
                context.nextListener <- outputEvent.Publish
        )

    let sink f el =
        let context =
            {
                actions = ResizeArray ()
                nextListener = null
            }

        match el with
        | Element f -> f context

        if context.nextListener <> null then
            if context.actions.Count > 0 then
                context.nextListener.Add f
            else
                let queue = Queue ()
                context.nextListener.Add queue.Enqueue
                context.actions.Add (fun () ->
                    while queue.Count > 0 do
                        f (queue.Dequeue ())
                )
            context.nextListener <- null

        context
