namespace rec Foom.Network

open System
open System.Collections.Generic

type ConnectedClientData =
    {
        heartbeatInterval : TimeSpan
        mutable heartbeatTime : TimeSpan
    }

type ClientData =
    {
        mutable pingTime : TimeSpan
        peerConnected : Event<IUdpEndPoint>
        peerDisconnected : Event<IUdpEndPoint>
    }

type ServerData =
    {
        connectionTimeout : TimeSpan
        peerLookup : Dictionary<IUdpEndPoint, ConnectedClientPeer>
        peerConnected : Event<IUdpEndPoint>
        peerDisconnected : Event<IUdpEndPoint>
    }

[<AutoOpen>]
module PeerHelpers =

    let createServerData connectionTimeout =
        {
            connectionTimeout = connectionTimeout
            peerLookup = Dictionary ()
            peerConnected = Event<IUdpEndPoint> ()
            peerDisconnected = Event<IUdpEndPoint> ()
        }

    let createClientData () =
        {
            pingTime = TimeSpan.Zero
            peerConnected = Event<IUdpEndPoint> ()
            peerDisconnected = Event<IUdpEndPoint> ()
        }

    let createConnectedClientData interval time =
        {
            heartbeatInterval = interval
            heartbeatTime = time
        }

[<RequireQualifiedAccess>]
type Udp =
    | Client of IUdpClient * ClientData
    | Server of IUdpServer * ServerData
    | ServerWithEndPoint of IUdpServer * IUdpEndPoint * ConnectedClientData

type Peer (udp : Udp) as this =

    let packetPool = PacketPool 1024
    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let sendStream = ByteStream (1024 * 1024)
    let sendWriter = ByteWriter (sendStream)

    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let receiverByteWriter = ByteWriter (receiveByteStream)

    // Senders
    let send = fun bytes size -> 
        match udp with
        | Udp.Client (client, _) -> client.Send (bytes, size) |> ignore
        | Udp.ServerWithEndPoint (server, endPoint, _) -> server.Send (bytes, size, endPoint) |> ignore
        | _ -> failwith "should not happen"

    let unreliableChan = UnreliableSender (packetPool, send)

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

    let connectionRequestedReceiver = ConnectionRequestedReceiver (packetPool, fun time packet endPoint ->
        match udp with
        | Udp.Server (udpServer, data) ->
            let peer = new ConnectedClientPeer (udpServer, endPoint, createConnectedClientData (TimeSpan.FromSeconds 1.) time)

            peer.LastReceiveTime <- time

            data.peerLookup.Add (endPoint, peer)

            let packet = Packet ()
            packet.Type <- PacketType.ConnectionAccepted
            udpServer.Send (packet.Raw, packet.Length, endPoint) |> ignore

            data.peerConnected.Trigger endPoint
        | _ -> ()
    )

    let connectionAcceptedReceiver = ConnectionAcceptedReceiver (packetPool, fun endPoint ->
        match udp with
        | Udp.Client (_, data) ->
            data.peerConnected.Trigger endPoint
        | _ -> ()
    )

    let receivers =
        [|
            connectionRequestedReceiver :> Receiver
            connectionAcceptedReceiver :> Receiver
            unreliableReceiver :> Receiver
            reliableOrderedReceiver :> Receiver
        |]

    member this.Udp = udp

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

    member val LastReceiveTime = TimeSpan.Zero with get, set

    member this.PacketPool = packetPool

    member this.Connect (address, port) =
        match udp with
        | Udp.Client (udpClient, _) ->
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
        | Udp.Server (_, data) ->
            data.peerLookup
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
        this.LastReceiveTime <- time

        match udp with
        | Udp.Server (server, data) ->
            match data.peerLookup.TryGetValue endPoint with
            | (true, peer) -> 
                // TODO: Disconnect the client if there are no more packets available in their pool.
                // TODO: Disconnect the client if too many packets were received in a time frame.

                // Trade packets from server and client
                peer.PacketPool.Get ()
                |> packetPool.Recycle

                peer.ReceivePacket (time, packet, endPoint)

            | _ ->

                match packet.Type with

                | PacketType.ConnectionRequested ->
                    connectionRequestedReceiver.Receive (time, packet, endPoint)

                | _ -> packetPool.Recycle packet

        | _ ->

            match packet.Type with

            | PacketType.ConnectionAccepted ->
                connectionAcceptedReceiver.Receive (time, packet, endPoint)

            | PacketType.Unreliable ->
                unreliableReceiver.Receive (time, packet, endPoint)

            | PacketType.ReliableOrdered ->
                reliableOrderedReceiver.Receive (time, packet, endPoint)

            | PacketType.ReliableOrderedAck ->
                packet.ReadAcks reliableOrderedChan.Ack
                packetPool.Recycle packet

            | PacketType.Ping ->

                let sendPacket = packetPool.Get ()
                sendPacket.Type <- PacketType.Pong
                sendPacket.Writer.Write<TimeSpan> (packet.Reader.Read<TimeSpan>())
                send sendPacket.Raw sendPacket.Length
                packetPool.Recycle sendPacket
                packetPool.Recycle packet

            | PacketType.Pong ->

                // TODO: Get ping time.
                packetPool.Recycle packet

            | PacketType.Disconnect ->

                match udp with
                | Udp.Client (client, data) ->
                    data.peerDisconnected.Trigger client.RemoteEndPoint
                | _ -> ()

                packetPool.Recycle packet

            | _ -> failwith "Packet type not supported."

    member this.Update time =
        receiveByteStream.Length <- 0

        match udp with
        | Udp.Client (client, _) ->

            while client.IsDataAvailable do
                let packet = packetPool.Get ()
                let byteCount = client.Receive (packet.Raw, 0, packet.Raw.Length)
                if byteCount > 0 then
                    packet.Length <- byteCount
                    this.ReceivePacket (time, packet, client.RemoteEndPoint)
                else
                    packetPool.Recycle packet

        | Udp.Server (server, data) ->

            while server.IsDataAvailable do
                let packet = packetPool.Get ()
                let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
                let byteCount = server.Receive (packet.Raw, 0, packet.Raw.Length, &endPoint)
                // TODO: Check to see if endPoint is banned.
                if byteCount > 0 then
                    packet.Length <- byteCount
                    this.ReceivePacket (time, packet, endPoint)
                else
                    packetPool.Recycle packet

            let endPointRemovals = Queue ()
            // Check for connection timeouts
            data.peerLookup
            |> Seq.iter (fun pair ->
                let endPoint = pair.Key
                let peer = pair.Value

                if time > peer.LastReceiveTime + data.connectionTimeout then

                    let packet = packetPool.Get ()
                    packet.Type <- PacketType.Disconnect
                    server.Send (packet.Raw, packet.Length, endPoint) |> ignore
                    packetPool.Recycle packet

                    (peer :> IDisposable).Dispose ()

                    endPointRemovals.Enqueue endPoint
            )

            while endPointRemovals.Count > 0 do
                let endPoint = endPointRemovals.Dequeue ()
                data.peerLookup.Remove endPoint |> ignore
                data.peerDisconnected.Trigger endPoint

        | Udp.ServerWithEndPoint (server, endPoint, data) ->

            if time > data.heartbeatTime + data.heartbeatInterval then

                data.heartbeatTime <- time

                let packet = packetPool.Get ()
                packet.Type <- PacketType.Ping
                packet.Writer.Write<TimeSpan> (time)
                send packet.Raw packet.Length
                packetPool.Recycle packet

        receivers
        |> Array.iter (fun receiver -> receiver.Update time)

        receiveByteStream.Position <- 0
        this.OnReceive receiverByteReader

        senders
        |> Array.iter (fun sender -> sender.Update time)

        match udp with
        | Udp.Server (_, data) ->
            data.peerLookup
            |> Seq.iter (fun pair ->
                let peer = pair.Value
                peer.Update time
            )
        | _ -> ()

        sendStream.Length <- 0

       // if packetPool.Count <> 1024 then
         //   failwithf "packet pool didn't go back to normal, 1024 <> %A." packetPool.Count


    interface IDisposable with

        member this.Dispose () =
            match udp with
            | Udp.Client (client, _) -> client.Dispose ()
            | Udp.Server (server, _) -> server.Dispose ()
            | _ -> ()

type ServerPeer (udpServer, connectionTimeout) =
    inherit Peer (Udp.Server (udpServer, createServerData connectionTimeout))

    member this.ClientConnected =
        match this.Udp with
        | Udp.Server (_, data) -> data.peerConnected.Publish
        | _ -> failwith "should not happen"

    member this.ClientDisconnected =
        match this.Udp with
        | Udp.Server (_, data) -> data.peerDisconnected.Publish
        | _ -> failwith "should not happen"

type ClientPeer (udpClient) =
    inherit Peer (Udp.Client (udpClient, createClientData ()))

    member this.Connected =
        match this.Udp with
        | Udp.Client (_, data) -> data.peerConnected.Publish
        | _ -> failwith "should not happen"

    member this.Disconnected =
        match this.Udp with
        | Udp.Client (_, data) -> data.peerDisconnected.Publish
        | _ -> failwith "should not happen"

type ConnectedClientPeer (udpServer, endPoint, data) =
    inherit Peer (Udp.ServerWithEndPoint (udpServer, endPoint, data))