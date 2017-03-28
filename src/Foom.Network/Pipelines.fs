[<AutoOpen>]
module internal Foom.Network.Pipelines

open Foom.Network
open Foom.Network.Pipeline

let unreliablePipeline f =
    let packetPool = PacketPool 64
    let source = UnreliableSource packetPool
    let merger = PacketMerger packetPool

    create source
    |> filter merger
    |> sink (fun packet ->
        f packet
        packetPool.Recycle packet
    )