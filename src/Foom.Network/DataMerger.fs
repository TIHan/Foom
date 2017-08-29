namespace Foom.Network

open System.Collections.Generic

[<AutoSerializable(false) ; NoEquality; NoComparison>]
type DataMerger =
    {
        dataQueue : Queue<struct (byte [] * int * int)>
    }

    member this.Enqueue (bytes, startIndex, size) =
        this.dataQueue.Enqueue (struct (bytes, startIndex, size))

    member this.Flush (packetPool : PacketPool, outputPackets) =
        while this.dataQueue.Count > 0 do
            let struct (bytes, startIndex, size) = this.dataQueue.Dequeue ()
            packetPool.GetFromBytes (bytes, startIndex, size, outputPackets)

    static member Create () =
        {
            dataQueue = Queue ()
        }