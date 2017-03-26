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
        packetPool : PacketPool
        queues : ResizeArray<IQueue>
        mutable nextListener : IObservable<Packet>
    }

type Element = Element of (ElementContext -> unit)

type SinkElement = SinkElement of (ElementContext -> unit)

module PipelineTest =

    type UnreliableSource (packetPool : PacketPool) =

        let outputEvent = Event<Packet> ()

        interface ISource with 

            member val Listen : IObservable<Packet> = outputEvent.Publish :> IObservable<Packet>

            member this.Send (data, startIndex, size) =
                let packet = packetPool.Get ()

                if size > packet.LengthRemaining then
                    failwith "Unreliable data is larger than what a new packet can hold. Consider using reliable sequenced."

                packet.SetData (PacketType.Unreliable, data, startIndex, size)
                outputEvent.Trigger packet

    type PacketMerger (packetPool : PacketPool) =

         let listenEvent = Event<Packet> ()

         let packets = ResizeArray<Packet> (packetPool.Amount)

         interface IQueue with

            member val Listen : IObservable<Packet> = listenEvent.Publish :> IObservable<Packet>

            member x.Send (packet : Packet) =
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


            member x.Flush () =
                packets
                |> Seq.iter (fun packet ->
                    listenEvent.Trigger packet
                )
                packets.Clear ()

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

    let sink f el =
        SinkElement (fun context ->
            match el with
            | Element f -> f context

            if context.nextListener <> null then
                context.nextListener.Add (fun packet ->
                    f packet
                    context.packetPool.Recycle packet
                )
                context.nextListener <- null
        )

    let build (sinkElement: SinkElement) =
        let context =
            {
                packetPool = PacketPool 64
                queues = ResizeArray ()
                nextListener = null
            }
        match sinkElement with
        | SinkElement f -> f context
        context

    let basicPipeline () =
        let packetPool = PacketPool 64
        let source = UnreliableSource packetPool
        let packetQueue = PacketMerger packetPool

        create source
        |> addQueue packetQueue
        |> sink (fun packet -> ())
