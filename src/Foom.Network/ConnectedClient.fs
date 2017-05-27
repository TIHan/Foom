namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) as this =

    let packetPool = PacketPool 1024
    let unreliable = unreliableSender packetPool

    let packetQueue = Queue<Packet> ()

    do
        unreliable.Output.Add (fun packet -> 
            this.SendNow (packet.Raw, packet.Length)
            packetPool.Recycle packet
        )

    member this.SendNow (data : byte [], size) =
        if size > 0 && data.Length > 0 then
            udpServer.Send (data, size, endPoint) |> ignore

    member this.Send (data, startIndex, size) =
        unreliable.Send { bytes = data; startIndex = startIndex; size = size }

    member this.SendConnectionAccepted () =
        let packet = Packet ()
        packet.SetData ([||], 0, 0)
        packet.PacketType <- PacketType.ConnectionAccepted

        packetQueue.Enqueue packet

    member this.Update () =

        while packetQueue.Count > 0 do
            let packet = packetQueue.Dequeue ()
            this.SendNow (packet.Raw, packet.Length)

        unreliable.Process ()
