namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type UnreliableChannel (endPoint: IUdpEndPoint) as this =

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))

    let outgoingQueue = Queue<Packet> ()
    let pendingQueue = Queue<Packet> ()

    let rec enqueueData data startIndex size =

        let packet = packetPool.Pop ()

        let sizeRemaining = packet.Raw.Length - packet.Length
        if sizeRemaining < size then
            failwith "Message is bigger than the size of an unreliable packet. Consider using a sequenced packet instead."
            //enqueueData data startIndex sizeRemaining
            //enqueueData data (startIndex + sizeRemaining) (size - sizeRemaining)
        else

        packet.SetData (PacketType.Unreliable, data, startIndex, size)

        if pendingQueue.Count > 1 then failwith "Pending Queue shouldn't have more than 1."
        if pendingQueue.Count = 1 then
            let peekPacket = pendingQueue.Peek ()
            if peekPacket.Raw.Length - peekPacket.Length > packet.Length then
                peekPacket.Merge packet
                this.RecyclePacket packet
            else
                outgoingQueue.Enqueue (pendingQueue.Dequeue ())
                pendingQueue.Enqueue packet
        else
            pendingQueue.Enqueue packet

    member this.EnqueueData (data, startIndex, size) =
        enqueueData data startIndex size

    member this.Process f =
        if pendingQueue.Count > 1 then failwith "Pending Queue shouldn't have more than 1."

        if pendingQueue.Count = 1 then
            outgoingQueue.Enqueue (pendingQueue.Dequeue ())

        while outgoingQueue.Count > 0 do
            let packet = outgoingQueue.Dequeue ()
            f packet
            this.RecyclePacket packet

        let x = ()
        ()

    member this.RecyclePacket (packet : Packet) =
        packet.Reset ()
        packetPool.Push packet

[<Sealed>]
type ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) =

    // Channels
    let unreliableChannel = UnreliableChannel (endPoint)

    // Extra Packet Queue
    let packetQueue = Queue<Packet> ()

    member this.SendNow (data : byte [], size) =
        if size > 0 && data.Length > 0 then
            udpServer.Send (data, size, endPoint) |> ignore

    member this.Send (data, startIndex, size) =
        unreliableChannel.EnqueueData (data, startIndex, size)

    member this.SendConnectionAccepted () =
        let packet = Packet ()
        packet.SetData (PacketType.ConnectionAccepted, [||], 0, 0)
        packetQueue.Enqueue packet

    member this.Update () =
        while packetQueue.Count > 0 do
            let packet = packetQueue.Dequeue ()
            this.SendNow (packet.Raw, packet.Length)

        unreliableChannel.Process (fun packet ->
            this.SendNow (packet.Raw, packet.Length)
        )

[<Sealed>]
type Server (udpServer: IUdpServer) =

    let recvPacket = Packet ()
    let sendStream = ByteStream (1024 * 1024 * 2)
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

    member val BytesSentSinceLastUpdate = 0 with get, set

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
        this.BytesSentSinceLastUpdate <- udpServer.BytesSentSinceLastCall ()
