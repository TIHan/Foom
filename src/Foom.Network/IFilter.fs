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

            packetPool.Recycle packet


        member x.Flush () =
            packets
            |> Seq.iter (fun packet ->
                listenEvent.Trigger packet
            )
            packets.Clear ()

type Sequencer () =

    let mutable seqN = 0us
    let outputEvent = Event<Packet> ()

    interface IPass with

        member val Listen : IObservable<Packet> = outputEvent.Publish :> IObservable<Packet>

        member x.Send packet =
            packet.SequenceId <- seqN
            seqN <- seqN + 1us
            outputEvent.Trigger packet

//http://gafferongames.com/networking-for-game-programmers/reliability-and-flow-control/
//bool sequence_more_recent( unsigned int s1, 
//                           unsigned int s2, 
//                           unsigned int max )
//{
//    return 
//        ( s1 > s2 ) && 
//        ( s1 - s2 <= max/2 ) 
//           ||
//        ( s2 > s1 ) && 
//        ( s2 - s1  > max/2 );
//}
[<AutoOpen>]
module AcksInternal =

    let sequenceMoreRecent (s1 : uint16) (s2 : uint16) =
        (s1 > s2) &&
        (s1 - s2 <= UInt16.MaxValue / 2us)
            ||
        (s2 > s1) &&
        (s2 - s1 > UInt16.MaxValue / 2us)

type AckManager () =

    let copyPacketPool = PacketPool (64)
    let copyPackets = Array.init 65536 (fun _ -> Unchecked.defaultof<Packet>)
    let mutable acks = Array.init 65536 (fun _ -> true)
    let mutable ackTimes = Array.init 65536 (fun _ -> DateTime ())

    let mutable newestAck = -1
    let mutable oldestAck = -1

    let pending = Queue ()

    member x.ForEachPending f =
        if newestAck = oldestAck then
            if not acks.[oldestAck] then
                f oldestAck ackTimes.[oldestAck]
        
        elif newestAck > oldestAck then
            for i = oldestAck to newestAck do
                if not acks.[i] then
                    f i ackTimes.[i]
        else
            for i = oldestAck to acks.Length - 1 do
                if not acks.[i] then
                    f i ackTimes.[i]

            for i = 0 to newestAck do
                if not acks.[i] then
                    f i ackTimes.[i]

    member x.Ack i =
        if not acks.[i] then
            copyPacketPool.Recycle copyPackets.[i]
            copyPackets.[i] <- Unchecked.defaultof<Packet>

            acks.[i] <- true
            ackTimes.[i] <- DateTime ()

            if oldestAck = i then
                oldestAck <- -1

            while oldestAck = -1 && pending.Count > 0 do
                let j = pending.Dequeue ()
                if not acks.[j] then
                    oldestAck <- j

    member x.Mark (packet : Packet) =
        let i = int packet.SequenceId
        if acks.[i] then
            let dt = DateTime.UtcNow
            let packet' = copyPacketPool.Get ()
            packet.CopyTo packet' 
            copyPackets.[i] <- packet'

            acks.[i] <- true
            ackTimes.[i] <- DateTime.UtcNow

            if oldestAck = -1 then
                oldestAck <- i

            if newestAck = -1 then
                newestAck <- i
            elif sequenceMoreRecent (uint16 i) (uint16 newestAck) then
                newestAck <- i

            pending.Enqueue i

type AckSetter (ackManager : AckManager) =

    let outputEvent = Event<Packet> ()

    interface IPass with

        member val Listen : IObservable<Packet> = outputEvent.Publish :> IObservable<Packet>

        member x.Send packet =
            ackManager.Mark packet

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

module PipelineTest =

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

    let basicPipeline f =
        let packetPool = PacketPool 64
        let source = UnreliableSource packetPool
        let merger = PacketMerger packetPool
        let merger2 = PacketMerger packetPool
        let merger3 = PacketMerger packetPool

        create source
        |> addQueue merger
        |> sink packetPool f
