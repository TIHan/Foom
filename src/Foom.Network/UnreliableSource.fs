namespace Foom.Network

open System

type UnreliableSource (packetPool : PacketPool) =

    let outputEvent = Event<Packet> ()

    interface ISource with 

        member val Listen : IObservable<Packet> = outputEvent.Publish :> IObservable<Packet>

        member this.Send (data, startIndex, size) =
            let packet = packetPool.Get ()

            if size > packet.LengthRemaining then
                failwith "Unreliable data is larger than what a new packet can hold. Consider using reliable sequenced."

            packet.SetData (data, startIndex, size)
            outputEvent.Trigger packet
