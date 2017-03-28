namespace Foom.Network

open System

[<Sealed>]
type AckSetter (ackManager : AckManager) =

    let outputEvent = Event<Packet> ()

    interface IFilter with

        member val Listen : IObservable<Packet> = outputEvent.Publish :> IObservable<Packet>

        member x.Send packet =
            ackManager.Mark packet

        member x.Process () = ()
