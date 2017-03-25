namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type UnreliableChannel (endPoint: IUdpEndPoint) =

    let outgoingQueue = Queue<Packet> ()

    member this.EnqueuePacket (packet: Packet) =
        outgoingQueue.Enqueue (packet)

    member this.Process f =
        while outgoingQueue.Count > 0 do
            let packet = outgoingQueue.Dequeue ()
            f packet

[<Sealed>]
type ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) as this =

    // Channels
    let unreliableChannel = UnreliableChannel (endPoint)

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))
    let mergedPacket = Packet ()

    member this.ResetMergedPacket () =
        mergedPacket.Reset ()
        mergedPacket.SetData (PacketType.Merged, [||], 0, 0)

    member this.SendPacket (packet: Packet) =
        match packet.PacketType with

        | PacketType.Unreliable ->
            unreliableChannel.EnqueuePacket packet

        | PacketType.ConnectionAccepted ->
            unreliableChannel.EnqueuePacket packet

        | _ -> ()

    member this.SendMergedPacketNow () =
        if mergedPacket.MergeCount > 0 then
            this.SendNow (mergedPacket.Raw, mergedPacket.Length)

    member this.MergePacket (packet : Packet) =
        if mergedPacket.Raw.Length - mergedPacket.Length > packet.Length then
            mergedPacket.Merge packet
        else
            this.SendMergedPacketNow ()
            this.ResetMergedPacket ()

    member this.SendNow (data, size) =
        udpServer.Send (data, size, endPoint) |> ignore

    member this.Send (data, startIndex, size) =
        let packet = packetPool.Pop ()
        packet.SetData (PacketType.Unreliable, data, startIndex, size)
        this.SendPacket packet

    member this.SendConnectionAccepted () =
        let packet = packetPool.Pop ()
        packet.SetData (PacketType.ConnectionAccepted, [||], 0, 0)
        this.SendPacket (packet)

    member this.RecyclePacket (packet: Packet) =
        packet.Reset ()
        packetPool.Push (packet)

    member this.Update () =
        this.ResetMergedPacket ()

        unreliableChannel.Process (fun packet ->
            this.MergePacket packet
            this.RecyclePacket packet
        )
        this.SendMergedPacketNow ()

[<Sealed>]
type Server (udpServer: IUdpServer) =

    let recvPacket = Packet ()
    let sendStream = ByteStream (64512)
    let sendWriter = ByteWriter (sendStream)

    let clients = ResizeArray<ConnectedClient> ()

    let clientConnected = Event<IUdpEndPoint> ()

    let onReceivePacket (packet : Packet) (endPoint: IUdpEndPoint) =
        match packet.PacketType with
        | PacketType.ConnectionRequested ->

            let client = ConnectedClient (endPoint, udpServer)

            clients.Add client

            client.SendConnectionAccepted ()

            clientConnected.Trigger endPoint

        | _ -> ()

    let receive () =
        while udpServer.IsDataAvailable do
            recvPacket.Reset ()

            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            match udpServer.Receive (recvPacket.Raw, 0, recvPacket.Raw.Length, &endPoint) with
            | 0 -> ()
            | byteCount ->
                recvPacket.Length <- byteCount
                onReceivePacket recvPacket endPoint

    let send () =
        clients
        |> Seq.iter (fun client -> client.Update ())

    [<CLIEvent>]
    member val ClientConnected = clientConnected.Publish

    member this.Publish<'T> (msg: 'T) =
        sendStream.Length <- 0

        match Network.lookup.TryGetValue typeof<'T> with
        | true, (id, serialize, _) ->
            sendWriter.WriteByte (byte id)
            serialize (msg :> obj) sendWriter
        | _ -> ()

        for i = 0 to clients.Count - 1 do
            clients.[i].Send (sendStream.Raw, 0, sendStream.Length)

    member this.Update () =
        receive ()
        send ()
