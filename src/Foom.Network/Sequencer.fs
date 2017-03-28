namespace Foom.Network

open System

type Sequencer () =

    let mutable seqN = 0us
    let outputEvent = Event<Packet> ()

    interface IFilter with

        member val Listen : IObservable<Packet> = outputEvent.Publish :> IObservable<Packet>

        member x.Send packet =
            packet.SequenceId <- seqN
            seqN <- seqN + 1us
            outputEvent.Trigger packet

        member x.Process () = ()
