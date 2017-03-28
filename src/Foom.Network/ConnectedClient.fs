namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) as this =

    //// Packet Pool
    //let packetPool = PacketPool (64)

    //// Packet Merger
    //let packetMerger = PacketMerger (packetPool)

    //// Channels
    //let unreliableChannel = UnreliableChannel (packetPool)

    let unreliablePipeline = 
        PipelineTest.basicPipeline (fun packet -> this.SendNow (packet.Raw, packet.Length))
        |> PipelineTest.build

    // Queues
    let packetQueue = Queue<Packet> ()

    member this.SendNow (data : byte [], size) =
        if size > 0 && data.Length > 0 then
            udpServer.Send (data, size, endPoint) |> ignore

    member this.Send (data, startIndex, size) =
        unreliablePipeline.Send (data, startIndex, size)
        //unreliableChannel.SendData (data, startIndex, size)

    member this.SendConnectionAccepted () =
        let packet = Packet ()
        packet.SetData ([||], 0, 0)
        packet.PacketType <- PacketType.ConnectionAccepted
        packetQueue.Enqueue packet

    member this.Update () =

        while packetQueue.Count > 0 do
            let packet = packetQueue.Dequeue ()
            this.SendNow (packet.Raw, packet.Length)

        unreliablePipeline.Flush ()

        //unreliableChannel.Flush (fun packet ->
        //   packetMerger.SendPacket packet
        //)

        //packetMerger.Flush (fun packet ->
        //    this.SendNow (packet.Raw, packet.Length)
        //)
