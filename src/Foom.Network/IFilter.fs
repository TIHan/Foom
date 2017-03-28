namespace Foom.Network

open System
open System.Collections.Generic

type ISource =

    abstract Send : byte [] * startIndex: int * size: int -> unit

    abstract Listen : IObservable<Packet>

type IPass =

    abstract Send : Packet -> unit

    abstract Listen : IObservable<Packet>

type IQueue =
    inherit IPass

    abstract Flush : unit -> unit

type ElementContext =
    {
        [<DefaultValue>] mutable source: byte [] -> int -> int -> unit
        queues : ResizeArray<IQueue>
        mutable nextListener : IObservable<Packet>
    }

    member this.Send (data, startIndex, size) =
        if obj.ReferenceEquals (this.source, null) |> not then
            this.source data startIndex size
     
    member this.Flush () =
        this.queues
        |> Seq.iter (fun x -> x.Flush ())

type Element = Element of (ElementContext -> unit)

type SinkElement = SinkElement of (ElementContext -> unit)

module Pipeline =

    let create (src: ISource) =

        Element (fun context ->
            context.source <- fun data startIndex size -> src.Send (data, startIndex, size)
            context.nextListener <- src.Listen
        )

    let addQueue (q : IQueue) el =
        Element (fun context ->
            match el with
            | Element f -> f context

            if context.nextListener <> null then
                context.nextListener.Add q.Send
                context.queues.Add q
                context.nextListener <- q.Listen
        )

    let sink (packetPool : PacketPool) f el =
        SinkElement (fun context ->
            match el with
            | Element f -> f context

            if context.nextListener <> null then
                context.nextListener.Add (fun packet ->
                    f packet
                    packetPool.Recycle packet
                )
                context.nextListener <- null
        )

    let build (sinkElement: SinkElement) =
        let context =
            {
                queues = ResizeArray ()
                nextListener = null
            }
        match sinkElement with
        | SinkElement f -> f context
        context
