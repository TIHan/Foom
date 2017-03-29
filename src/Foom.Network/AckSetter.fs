namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type AckSetter (ackManager : AckManager) =

    let packets = Queue<Packet> ()

    interface IFilter with

        member x.Send packet =
            packets.Enqueue packet

        member x.Process output =
            while packets.Count > 0 do
                let packet = packets.Dequeue ()
                ackManager.MarkCopy packet

