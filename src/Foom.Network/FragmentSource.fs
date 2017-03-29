namespace rec Foom.Network

open System

type FragmentSource =
    {
        packetPool  : PacketPool
    }

    interface ISource with

        member x.Send (data, startIndex, size, output) = send data startIndex size output x

    static member Create packetPool =
        {
            packetPool = packetPool
        }

[<AutoOpen>]
module FragmentSourceHelpers =

    let send data startIndex size output src =
        let numberOfPackets = int <| Math.Ceiling (double size / double NetConstants.PacketSize)

        let mutable currentSize = size
        for i = 0 to numberOfPackets - 1 do
            let packet = src.packetPool.Get ()

            if i = numberOfPackets - 1 then
                packet.SetData (data, startIndex + (NetConstants.PacketSize * i), currentSize)
            else
                packet.SetData (data, startIndex + (NetConstants.PacketSize * i), NetConstants.PacketSize)

            output packet
            currentSize <- currentSize - NetConstants.PacketSize
