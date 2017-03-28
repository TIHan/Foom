namespace Foom.Network

open System
open System.Collections.Generic

type ISource =

    abstract Send : byte [] * startIndex: int * size: int -> unit

    abstract Listen : IObservable<Packet>

type IFilter =

    abstract Send : Packet -> unit

    abstract Listen : IObservable<Packet>

    abstract Process : unit -> unit

type Pipeline =
    {
        [<DefaultValue>] mutable source: byte [] -> int -> int -> unit
        filters : ResizeArray<IFilter>
        mutable nextListener : IObservable<Packet>
    }

    member this.Send (data, startIndex, size) =
        if obj.ReferenceEquals (this.source, null) |> not then
            this.source data startIndex size
     
    member this.Process () =
        this.filters
        |> Seq.iter (fun x -> x.Process ())

type Element = Element of (Pipeline -> unit)

module Pipeline =

    let create (src : ISource) =
        Element (fun context ->
            context.source <- fun data startIndex size -> src.Send (data, startIndex, size)
            context.nextListener <- src.Listen
        )

    let filter (filter : IFilter) el =
        Element (fun context ->
            match el with
            | Element f -> f context

            if context.nextListener <> null then
                context.nextListener.Add filter.Send
                context.filters.Add filter
                context.nextListener <- filter.Listen
        )

    let sink f el =
        let context =
            {
                filters = ResizeArray ()
                nextListener = null
            }

        match el with
        | Element f -> f context

        if context.nextListener <> null then
            context.nextListener.Add (fun packet ->
                f packet
            )
            context.nextListener <- null

        context
