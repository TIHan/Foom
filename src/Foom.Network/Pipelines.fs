[<AutoOpen>]
module internal Foom.Network.Pipelines

open Foom.Network
open Foom.Network.Pipeline

let ReceiverSource (packetPool : PacketPool) =

    { new ISource with 

        member x.Send (data, startIndex, size, output) =
            let packet = packetPool.Get ()
            packet.Set (data, startIndex, size)
            output packet }

let unreliableSender f =
    let packetPool = PacketPool 64
    let source = UnreliableSource packetPool
    let merger = PacketMerger packetPool

    create source
    |> add merger
    |> sink (fun packet ->
        f packet
        packetPool.Recycle packet
    )

let basicReceiver f =
    let packetPool = PacketPool 64
    let source = ReceiverSource packetPool

    create source
    |> sink (fun packet ->
        f packet
        packetPool.Recycle packet
    )