namespace Foom.Network

open System
open System.Collections.Generic

type Sequencer () =

    let mutable seqN = 0us
    let packets = Queue ()

    interface IFilter with

        member x.Send packet = 
            packets.Enqueue packet

        member x.Process output =
            while packets.Count > 0 do
                let packet = packets.Dequeue ()
                packet.SequenceId <- seqN
                seqN <- seqN + 1us
                output packet