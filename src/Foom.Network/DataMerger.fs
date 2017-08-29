namespace Foom.Network

open System
open System.Collections.Generic

[<AutoOpen>]
module DataMergerImpl =

    let inline setFragmentId (fragmentId : byref<byte>) (packet : Packet) =
        packet.FragmentId <- fragmentId
        fragmentId <- fragmentId + 1uy

    let inline setFragment count (fragmentId : byref<byte>) packet =
        setFragmentId &fragmentId packet
        packet.FragmentCount <- count

    let inline createFragmentPacket count (fragmentId : byref<byte>) (packetPool : PacketPool) =
        let packet = packetPool.Get ()
        setFragment (byte count) &fragmentId packet
        packet

    let fragment (bytes : byte []) (startIndex : int) (size : int) (packet : Packet) (packets : ResizeArray<Packet>) (packetPool : PacketPool) =
        let count = (size / int packet.DataLengthRemaining) + (if size % int packet.DataLengthRemaining > 0 then 1 else 0)

        if count > 255 then
            failwith "Fragmented count for data is greater than 255."

        let mutable startIndex = startIndex
        let mutable fragmentId = 1uy

        setFragment (byte count) &fragmentId packet

        packet.Write (bytes, startIndex, int packet.DataLengthRemaining)
        packets.Add packet

        for i = 1 to count - 1 do
            let packet = createFragmentPacket (byte count) &fragmentId packetPool

            if i = (count - 1) then
                let startIndex = startIndex + (i * int packet.DataLengthRemaining)
                packet.Write (bytes, startIndex, size - startIndex)
            else
                packet.Write (bytes, startIndex + (i * int packet.DataLengthRemaining), int packet.DataLengthRemaining)

            packets.Add packet 

    let getFromBytes bytes startIndex (size : int) (packets : ResizeArray<Packet>) (packetPool : PacketPool) =
        if packets.Count = 0 then
            let packet = packetPool.Get ()
            if int packet.DataLengthRemaining >= size then
                packet.Write (bytes, startIndex, size)
                packets.Add packet
            else
                fragment bytes startIndex size packet packets packetPool
                    
        else
            let packet = packets.[packets.Count - 1]
            if int packet.DataLengthRemaining >= size then
                packet.Write (bytes, startIndex, size)
            else
                let packet = packetPool.Get ()
                if int packet.DataLengthRemaining >= size then
                    packet.Write (bytes, startIndex, size)
                    packets.Add packet
                else
                    fragment bytes startIndex size packet packets packetPool

type IFilter<'Input, 'Output> =

    abstract Enqueue : 'Input -> unit

    abstract Flush : TimeSpan * ('Output -> unit) -> unit

    abstract Reset : unit -> unit

module Filter =

    let combine (filter2 : IFilter<'Output, 'NewOutput>) (filter1 : IFilter<'Input, 'Output>) : IFilter<'Input, 'NewOutput> =
        {
            new IFilter<'Input, 'NewOutput> with

                member __.Enqueue input = filter1.Enqueue input

                member __.Flush (time, f) =
                    filter1.Flush (time, fun x -> filter2.Enqueue x)
                    filter2.Flush (time, f)

                member __.Reset () =
                    filter1.Reset ()
                    filter2.Reset ()
        }

    let outputMap (mapping : TimeSpan -> 'Output -> 'NewOutput) (filter : IFilter<'Input, 'Output>) : IFilter<'Input, 'NewOutput> =
        {
            new IFilter<'Input, 'NewOutput> with

                member __.Enqueue input = filter.Enqueue input

                member __.Flush (time, f) =
                    filter.Flush (time, fun x -> f (mapping time x))

                member __.Reset () =
                    filter.Reset ()
        }

    let inputMap (mapping : 'NewInput -> 'Input) (filter : IFilter<'Input, 'Output>) : IFilter<'NewInput, 'Output> =
        {
            new IFilter<'NewInput, 'Output> with

                member __.Enqueue input = filter.Enqueue (mapping input)

                member __.Flush (time, f) =
                    filter.Flush (time, f)

                member __.Reset () =
                    filter.Reset ()
        }

    let reset f (filter : IFilter<'Input, 'Output>) =
        {
            new IFilter<'Input, 'Output> with

                member __.Enqueue input = filter.Enqueue input

                member __.Flush (time, f) = filter.Flush (time, f)

                member __.Reset () =
                    filter.Reset ()
                    f ()

        }

    let flush flushing (filter : IFilter<'Input, 'Output>) =
        {
            new IFilter<'Input, 'Output> with

                member __.Enqueue input = filter.Enqueue input

                member __.Flush (time, f) =
                    filter.Flush (time, f)

                    flushing time f

                member __.Reset () =
                    filter.Reset ()
        }

[<Sealed>]
type DataMerger (packetPool : PacketPool, dataQueue : Queue<struct (byte [] * int * int)>) =

    let outputPackets = ResizeArray ()

    interface IFilter<struct (byte [] * int * int), Packet> with

        member __.Enqueue input =
            dataQueue.Enqueue input

        member __.Flush (_, f) =
            while dataQueue.Count > 0 do
                let struct (bytes, startIndex, size) = dataQueue.Dequeue ()
                getFromBytes bytes startIndex size outputPackets packetPool

            for i = 0 to outputPackets.Count - 1 do
                f outputPackets.[i]

            outputPackets.Clear ()

        member __.Reset () =
            dataQueue.Clear ()

    static member Create packetPool = DataMerger (packetPool, Queue ())

        