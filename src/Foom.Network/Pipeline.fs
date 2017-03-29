namespace Foom.Network

open System
open System.Collections.Generic

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
