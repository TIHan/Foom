[<AutoOpen>]
module internal Foom.Network.Pipelines

open Foom.Network
open Foom.Network.Pipeline

let unreliablePipeline f =
    let packetPool = PacketPool 64
    let source = UnreliableSource packetPool
    let merger = PacketMerger packetPool

    create source
    |> addQueue merger
    |> sink packetPool f