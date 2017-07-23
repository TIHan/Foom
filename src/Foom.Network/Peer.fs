namespace Foom.Network

open System
open System.Collections.Generic

[<RequireQualifiedAccess>]
type Udp =
    | Client of IUdpClient
    | Server of IUdpServer
    | ServerWithEndPoint of IUdpServer * IUdpEndPoint

type Peer (udp : Udp, packetPool : PacketPool) as this =

    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let sendStream = ByteStream (1024 * 1024)
    let sendWriter = ByteWriter (sendStream)

    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let receiverByteWriter = ByteWriter (receiveByteStream)

    let peerLookup = Dictionary<IUdpEndPoint, Peer> ()

    let peerConnected = Event<IUdpEndPoint> ()

    // Senders
    let send = fun bytes size -> 
        match udp with
        | Udp.Client client -> client.Send (bytes, size) |> ignore
        | Udp.ServerWithEndPoint (server, endPoint) -> server.Send (bytes, size, endPoint) |> ignore
        | _ -> failwith "should not happen"

    let unreliableChan = UnreliableChannel (packetPool, send)

    let reliableOrderedChan = ReliableOrderedChannel (packetPool, send)

    let reliableOrderedAckSender = ReliableOrderedAckSender (packetPool, send)

    let senders =
        [|
            unreliableChan :> Channel
            reliableOrderedChan :> Channel
            reliableOrderedAckSender :> Channel
        |]

    // Receivers
    let unreliableReceiver = UnreliableReceiver (packetPool, fun packet endPoint ->
        receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.DataLength)
    )

    let reliableOrderedReceiver = 
        ReliableOrderedReceiver (packetPool, 
            (fun ack -> 
                let startIndex = sendStream.Position
                sendWriter.WriteUInt16 ack
                let size = sendStream.Position - startIndex
                this.Send (sendStream.Raw, startIndex, size, PacketType.ReliableOrderedAck)
            ), fun packet ->
                receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.DataLength)
       )

    let connectionRequestedReceiver = ConnectionRequestedReceiver (packetPool, fun packet endPoint ->
        match udp with
        | Udp.Server udpServer ->
            let peer = Peer (Udp.ServerWithEndPoint (udpServer, endPoint), packetPool)

            peerLookup.Add (endPoint, peer)

            let packet = Packet ()
            packet.Type <- PacketType.ConnectionAccepted
            udpServer.Send (packet.Raw, packet.Length, endPoint) |> ignore

            peerConnected.Trigger endPoint
        | _ -> ()
    )

    let connectionAcceptedReceiver = ConnectionAcceptedReceiver (packetPool, fun endPoint ->
        peerConnected.Trigger endPoint
    )

    let receivers =
        [|
            connectionRequestedReceiver :> Receiver
            connectionAcceptedReceiver :> Receiver
            unreliableReceiver :> Receiver
            reliableOrderedReceiver :> Receiver
        |]

    member private this.OnReceive (reader : ByteReader) =

        let rec onReceive (reader : ByteReader) =
            let typeId = reader.ReadByte () |> int

            if subscriptions.Length > typeId && typeId >= 0 then
                let pickler = Network.FindTypeById typeId
                let msg = pickler.ctor reader
                pickler.deserialize msg reader
                subscriptions.[typeId].Trigger msg
            else
                failwith "This shouldn't happen."

        while not reader.IsEndOfStream do
            onReceive reader

    member this.PeerConnected = peerConnected.Publish

    member this.Connect (address, port) =
        match udp with
        | Udp.Client udpClient ->
            if udpClient.Connect (address, port) then
                let packet = Packet ()
                packet.Type <- PacketType.ConnectionRequested
                udpClient.Send (packet.Raw, packet.Length) |> ignore
        | _ -> failwith "Clients can only connect."

    member this.Subscribe<'T> f =
        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let evt = subscriptions.[id]
            let pickler = Network.FindTypeById id

            evt.Publish.Add (fun msg -> f (msg :?> 'T))
        | _ -> ()

    member this.Send (bytes, startIndex, size, packetType) =
        match udp with
        | Udp.Server _ ->
            peerLookup
            |> Seq.iter (fun pair ->
                let peer = pair.Value
                peer.Send (bytes, startIndex, size, packetType)
            )
        | _ ->

            match packetType with
            | PacketType.Unreliable ->
                unreliableChan.Send (bytes, startIndex, size)

            | PacketType.ReliableOrdered ->
                reliableOrderedChan.Send (bytes, startIndex, size)

            | PacketType.ReliableOrderedAck ->
                reliableOrderedAckSender.Send (bytes, startIndex, size)

            | _ -> failwith "packet type not supported"

    member private this.Send<'T> (msg : 'T, packetType) =
        let startIndex = sendStream.Position

        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let pickler = Network.FindTypeById id
            sendWriter.WriteByte (byte id)
            pickler.serialize (msg :> obj) sendWriter

            let size = sendStream.Position - startIndex

            this.Send (sendStream.Raw, startIndex, size, packetType)

        | _ -> ()

    member this.SendUnreliable<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.Unreliable)

    member this.SendReliableOrdered<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.ReliableOrdered)

    member this.ReceivePacket (time, packet : Packet, endPoint : IUdpEndPoint) =
        match peerLookup.TryGetValue endPoint with
        | true, peer -> peer.ReceivePacket (time, packet, endPoint)
        | _ ->

            match packet.Type with

            | PacketType.ConnectionRequested ->
                connectionRequestedReceiver.Receive (time, packet, endPoint)

            | PacketType.ConnectionAccepted ->
                connectionAcceptedReceiver.Receive (time, packet, endPoint)

            | PacketType.Unreliable ->
                unreliableReceiver.Receive (time, packet, endPoint)

            | PacketType.ReliableOrdered ->
                reliableOrderedReceiver.Receive (time, packet, endPoint)

            | PacketType.ReliableOrderedAck ->
                packet.ReadAcks reliableOrderedChan.Ack
                packetPool.Recycle packet

            | _ -> failwith "Packet type not supported."

    member this.Update time =
        receiveByteStream.Length <- 0

        match udp with
        | Udp.Client client ->

            while client.IsDataAvailable do
                let packet = packetPool.Get ()
                let byteCount = client.Receive (packet.Raw, 0, packet.Raw.Length)
                if byteCount > 0 then
                    packet.Length <- byteCount
                    this.ReceivePacket (time, packet, client.RemoteEndPoint)
                else
                    packetPool.Recycle packet

        | Udp.Server server ->

            while server.IsDataAvailable do
                let packet = packetPool.Get ()
                let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
                let byteCount = server.Receive (packet.Raw, 0, packet.Raw.Length, &endPoint)
                if byteCount > 0 then
                    packet.Length <- byteCount
                    this.ReceivePacket (time, packet, endPoint)
                else
                    packetPool.Recycle packet

        | Udp.ServerWithEndPoint _ -> ()

        receivers
        |> Array.iter (fun receiver -> receiver.Update time)

        receiveByteStream.Position <- 0
        this.OnReceive receiverByteReader

        senders
        |> Array.iter (fun sender -> sender.Update time)

        peerLookup
        |> Seq.iter (fun pair ->
            let peer = pair.Value
            peer.Update time
        )

        sendStream.Length <- 0

       // if packetPool.Count <> 1024 then
         //   failwithf "packet pool didn't go back to normal, 1024 <> %A." packetPool.Count
