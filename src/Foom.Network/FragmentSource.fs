namespace rec Foom.Network

open System

type FragmentSource =
    {
        outputEvent : Event<Packet>
        packetPool  : PacketPool
    }

    interface ISource with

        member x.Listen = listen x

        member x.Send (data, startIndex, size) = send data startIndex size x

    static member Create packetPool =
        {
            outputEvent = Event<Packet> ()
            packetPool = packetPool
        }

[<AutoOpen>]
module FragmentSourceHelpers =

    let listen src = src.outputEvent.Publish :> IObservable<_>

    let send data startIndex size src =
        let numberOfPackets = int <| Math.Ceiling (double size / double NetConstants.PacketSize)

        let mutable currentSize = size
        for i = 0 to numberOfPackets - 1 do
            let packet = src.packetPool.Get ()

            if i = numberOfPackets - 1 then
                packet.SetData (data, startIndex + (NetConstants.PacketSize * i), currentSize)
            else
                packet.SetData (data, startIndex + (NetConstants.PacketSize * i), NetConstants.PacketSize)

            src.outputEvent.Trigger packet
            currentSize <- currentSize - NetConstants.PacketSize
