namespace Foom.Network

open System
open System.IO
open System.Collections.Generic
open System.IO.Compression

type SendStreamState =
    {
        sendStream :    ByteStream
    }

module SendStreamState =

    let create () =
        {
            sendStream = new ByteStream (Array.zeroCreate <| 1024 * 1024)
        }

    let write (msg : 'T) f (state : SendStreamState) =

        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let sendStream = state.sendStream

            let startIndex = int sendStream.Position

            let pickler = Network.FindTypeById id
            sendStream.Writer.WriteByte (byte id)
            pickler.serialize (msg :> obj) sendStream.Writer

            let size = int sendStream.Position - startIndex

            f state.sendStream.Raw startIndex (int size)

        | _ -> ()

type ReceiveStreamState =
    {
        receiveStream : ByteStream
    }

module ReceiveStreamState =

    let create () =
        {
            receiveStream = new ByteStream (Array.zeroCreate <| 1024 * 1024)
        }

    let rec read' (subscriptions : Event<obj> []) (state : ReceiveStreamState) =
        let reader = state.receiveStream.Reader
        let typeId = reader.ReadByte () |> int

        if subscriptions.Length > typeId && typeId >= 0 then
            let pickler = Network.FindTypeById typeId
            let msg = pickler.ctor reader
            pickler.deserialize msg reader
            subscriptions.[typeId].Trigger msg
        else
            failwith "This shouldn't happen."

    let read subscriptions state =
        state.receiveStream.Position <- 0L

        while not state.receiveStream.Reader.IsEndOfStream do
            read' subscriptions state

[<AutoOpen>]
module StateHelpers =

    let createSubscriptions () =
        Array.init 1024 (fun _ -> Event<obj> ())

type ClientState =
    {
        packetPool : PacketPool
        sendStreamState : SendStreamState
        receiveStreamState : ReceiveStreamState
        subscriptions : Event<obj> []
        udpClient : IUdpClient
        connectionTimeout : TimeSpan
        peerConnected : Event<IUdpEndPoint>
        peerDisconnected : Event<IUdpEndPoint>
        mutable lastReceiveTime : TimeSpan

        basicChannelState : BasicChannelState
    }

module ClientState =

    let create (udpClient : IUdpClient) connectionTimeout =

        let packetPool = PacketPool 1024
        let sendStreamState = SendStreamState.create ()
        let receiveStreamState = ReceiveStreamState.create ()

        let basicChannelState =
            BasicChannelState.create packetPool
                (fun packet ->
                    receiveStreamState.receiveStream.Write (packet.Raw, sizeof<PacketHeader>, int packet.DataLength)
                )
                udpClient.Send
                (fun ack send ->
                    let startIndex = int sendStreamState.sendStream.Position
                    sendStreamState.sendStream.Writer.WriteUInt16 ack
                    let size = int sendStreamState.sendStream.Position - startIndex
                    send (sendStreamState.sendStream.Raw, startIndex, size)
                )

        {
            packetPool = packetPool
            sendStreamState = sendStreamState
            receiveStreamState = receiveStreamState
            subscriptions = createSubscriptions ()
            udpClient = udpClient
            connectionTimeout = connectionTimeout
            peerConnected = Event<IUdpEndPoint> ()
            peerDisconnected = Event<IUdpEndPoint> ()
            basicChannelState = basicChannelState
            lastReceiveTime = TimeSpan.Zero
        }

    let receive time (packet : Packet) (state : ClientState) =
        match packet.Type with

        | PacketType.ConnectionAccepted ->
            state.peerConnected.Trigger (state.udpClient.RemoteEndPoint)
            state.packetPool.Recycle packet
            true

        | PacketType.Ping ->
            let sendPacket = state.packetPool.Get ()
            sendPacket.Type <- PacketType.Pong
            sendPacket.Writer.Write<TimeSpan> (packet.Reader.Read<TimeSpan>())
            state.udpClient.Send packet
            state.packetPool.Recycle sendPacket
            state.packetPool.Recycle packet
            true

        | PacketType.Disconnect ->
            state.peerDisconnected.Trigger (state.udpClient.RemoteEndPoint)
            state.packetPool.Recycle packet
            true

        | _ -> false

type ConnectedClientState =
    {
        packetPool : PacketPool
        sendStreamState : SendStreamState
        receiveStreamState : ReceiveStreamState
        endPoint : IUdpEndPoint
        heartbeatInterval : TimeSpan
        mutable heartbeatTime : TimeSpan
        mutable pingTime : TimeSpan
        mutable lastReceiveTime : TimeSpan

        basicChannelState : BasicChannelState
    }

module ConnectedClientState =

    let create (udpServer : IUdpServer) endPoint heartbeatInterval time =

        let packetPool = PacketPool 1024
        let sendStreamState = SendStreamState.create ()
        let receiveStreamState = ReceiveStreamState.create ()

        let basicChannelState =
            BasicChannelState.create packetPool
                (fun packet ->
                    receiveStreamState.receiveStream.Write (packet.Raw, sizeof<PacketHeader>, int packet.DataLength)
                )
                (fun packet -> udpServer.Send (packet, endPoint))
                (fun ack send ->
                    let startIndex = int sendStreamState.sendStream.Position
                    sendStreamState.sendStream.Writer.WriteUInt16 ack
                    let size = int sendStreamState.sendStream.Position - startIndex
                    send (sendStreamState.sendStream.Raw, startIndex, size)
                )

        {
            packetPool = packetPool
            sendStreamState = sendStreamState
            receiveStreamState = receiveStreamState
            endPoint = endPoint
            heartbeatInterval = heartbeatInterval
            heartbeatTime = time
            pingTime = TimeSpan.Zero
            lastReceiveTime = time
            basicChannelState = basicChannelState
        }

    let receive time (packet : Packet) state =
        match packet.Type with
        | PacketType.Pong ->

            state.pingTime <- time - packet.Reader.Read<TimeSpan> ()
            state.packetPool.Recycle packet
            true
        | _ -> false

type ServerState =
    {
        packetPool : PacketPool
        sendStreamState : SendStreamState
        subscriptions : Event<obj> []
        udpServer : IUdpServer
        connectionTimeout : TimeSpan
        peerLookup : Dictionary<IUdpEndPoint, ConnectedClientState>
        peerConnected : Event<IUdpEndPoint>
        peerDisconnected : Event<IUdpEndPoint>
    }

module ServerState =

    let create udpServer connectionTimeout =

        let packetPool = PacketPool 1024
        let sendStreamState = SendStreamState.create ()

        {
            packetPool = packetPool
            sendStreamState = sendStreamState
            subscriptions = createSubscriptions ()
            udpServer = udpServer
            connectionTimeout = connectionTimeout
            peerLookup = Dictionary ()
            peerConnected = Event<IUdpEndPoint> ()
            peerDisconnected = Event<IUdpEndPoint> ()
        }

    let receive time (packet : Packet) endPoint state =
        match packet.Type with
        | PacketType.ConnectionRequested ->
            let ccState = ConnectedClientState.create state.udpServer endPoint (TimeSpan.FromSeconds 1.) time

            state.peerLookup.Add (endPoint, ccState)

            use tmp = new Packet ()
            tmp.Type <- PacketType.ConnectionAccepted
            state.udpServer.Send (tmp, endPoint)
            state.packetPool.Recycle packet
            state.peerConnected.Trigger endPoint
            true

        | PacketType.Disconnect ->
            
            state.peerLookup.Remove endPoint |> ignore
            state.packetPool.Recycle packet
            true
        | _ -> false

[<RequireQualifiedAccess>]
type Udp =
    | Client of ClientState
    | Server of ServerState

[<AbstractClass>]
type Peer (udp : Udp) =

    member this.Udp = udp

    member this.PacketPool = 
        match udp with
        | Udp.Client state -> state.packetPool
        | Udp.Server state -> state.packetPool

    member this.Connect (address, port) =
        match udp with
        | Udp.Client state ->
            if state.udpClient.Connect (address, port) then
                use tmp = new Packet ()
                tmp.Type <- PacketType.ConnectionRequested
                state.udpClient.Send tmp
        | _ -> failwith "Clients can only connect."

    member this.Subscribe<'T> f =
        let subscriptions =
            match udp with
            | Udp.Client state -> state.subscriptions
            | Udp.Server state -> state.subscriptions

        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let evt = subscriptions.[id]
            let pickler = Network.FindTypeById id

            evt.Publish.Add (fun msg -> f (msg :?> 'T))
        | _ -> ()

    member this.Send (bytes, startIndex, size, packetType) =
        match udp with
        | Udp.Server state ->
            state.peerLookup
            |> Seq.iter (fun pair ->
                let ccState = pair.Value
                BasicChannelState.send bytes startIndex size packetType ccState.basicChannelState
            )

        | Udp.Client state ->
            BasicChannelState.send bytes startIndex size packetType state.basicChannelState

    member private this.Send<'T> (msg : 'T, packetType) =
        match udp with
        | Udp.Client state -> state.sendStreamState
        | Udp.Server state -> state.sendStreamState
        |> SendStreamState.write msg (fun bytes startIndex size -> 
            this.Send (bytes, startIndex, size, packetType)
        )

    member this.SendUnreliable<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.Unreliable)

    member this.SendReliableOrdered<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.ReliableOrdered)

    member this.ReceivePacket (time, packet : Packet, endPoint : IUdpEndPoint) =
        match udp with
        | Udp.Client state -> 
            state.lastReceiveTime <- time

            match BasicChannelState.receive time packet state.basicChannelState with
            | false ->
                match ClientState.receive time packet state with
                | false -> state.packetPool.Recycle packet
                | _ -> ()
            | _ -> ()

        | Udp.Server state ->
            match state.peerLookup.TryGetValue endPoint with
            | (true, ccState) -> 
                ccState.lastReceiveTime <- time

                // TODO: Disconnect the client if there are no more packets available in their pool.
                // TODO: Disconnect the client if too many packets were received in a time frame.

                // Trade packets from server and client
                ccState.packetPool.Get ()
                |> state.packetPool.Recycle

                match BasicChannelState.receive time packet ccState.basicChannelState with
                | false ->
                    match ConnectedClientState.receive time packet ccState with
                    | false -> ccState.packetPool.Recycle packet
                    | _ -> ()
                | _ -> ()
                
            | _ ->

                match ServerState.receive time packet endPoint state with
                | false -> state.packetPool.Recycle packet
                | _ -> ()

    member this.Update time =
        match udp with
        | Udp.Client state ->

            state.receiveStreamState.receiveStream.SetLength 0L

            let client = state.udpClient
            let packetPool = state.packetPool

            while client.IsDataAvailable do
                let packet = packetPool.Get ()
                let byteCount = client.Receive (packet)
                if byteCount > 0 then
                    this.ReceivePacket (time, packet, client.RemoteEndPoint)
                else
                    packetPool.Recycle packet

            state.basicChannelState.UpdateReceive time

            ReceiveStreamState.read state.subscriptions state.receiveStreamState

            state.basicChannelState.UpdateSend time

            state.sendStreamState.sendStream.SetLength 0L

            if time > state.lastReceiveTime + state.connectionTimeout then
                state.peerDisconnected.Trigger state.udpClient.RemoteEndPoint

        | Udp.Server state ->

            let server = state.udpServer
            let packetPool = state.packetPool

            while server.IsDataAvailable do
                let packet = packetPool.Get ()
                let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
                let byteCount = server.Receive (packet, &endPoint)
                // TODO: Check to see if endPoint is banned.
                if byteCount > 0 then
                    this.ReceivePacket (time, packet, endPoint)
                else
                    packetPool.Recycle packet

            let endPointRemovals = Queue ()
            // Check for connection timeouts
            state.peerLookup
            |> Seq.iter (fun pair ->
                let endPoint = pair.Key
                let ccState = pair.Value

                if time > ccState.lastReceiveTime + state.connectionTimeout then

                    let packet = packetPool.Get ()
                    packet.Type <- PacketType.Disconnect
                    server.Send (packet, endPoint)
                    packetPool.Recycle packet

                    endPointRemovals.Enqueue endPoint

                if time > ccState.heartbeatTime + ccState.heartbeatInterval then

                    ccState.heartbeatTime <- time

                    let packet = packetPool.Get ()
                    packet.Type <- PacketType.Ping
                    packet.Writer.Write<TimeSpan> (time)
                    server.Send (packet, endPoint)
                    packetPool.Recycle packet

                ccState.basicChannelState.UpdateReceive time

                ReceiveStreamState.read state.subscriptions ccState.receiveStreamState
            
                ccState.basicChannelState.UpdateSend time

                ccState.sendStreamState.sendStream.SetLength 0L
            )

            state.sendStreamState.sendStream.SetLength 0L

            while endPointRemovals.Count > 0 do
                let endPoint = endPointRemovals.Dequeue ()
                state.peerLookup.Remove endPoint |> ignore
                state.peerDisconnected.Trigger endPoint


    interface IDisposable with

        member this.Dispose () =
            match udp with
            | Udp.Client state -> state.udpClient.Dispose ()
            | Udp.Server state-> state.udpServer.Dispose ()

type ServerPeer (udpServer, connectionTimeout) =
    inherit Peer (Udp.Server (ServerState.create udpServer connectionTimeout))

    member this.ClientConnected =
        match this.Udp with
        | Udp.Server state -> state.peerConnected.Publish
        | _ -> failwith "should not happen"

    member this.ClientDisconnected =
        match this.Udp with
        | Udp.Server state -> state.peerDisconnected.Publish
        | _ -> failwith "should not happen"

    member this.ClientPacketPoolMaxCount =
        match this.Udp with
        | Udp.Server server -> server.peerLookup |> Seq.sumBy (fun pair1 -> pair1.Value.packetPool.MaxCount)
        | _ -> failwith "nope"

    member this.ClientPacketPoolCount =
        match this.Udp with
        | Udp.Server server -> server.peerLookup |> Seq.sumBy (fun pair1 -> pair1.Value.packetPool.Count)
        | _ -> failwith "nope"

type ClientPeer (udpClient) =
    inherit Peer (Udp.Client (ClientState.create udpClient (TimeSpan.FromSeconds 5.)))

    member this.Connected =
        match this.Udp with
        | Udp.Client state -> state.peerConnected.Publish
        | _ -> failwith "should not happen"

    member this.Disconnected =
        match this.Udp with
        | Udp.Client state -> state.peerDisconnected.Publish
        | _ -> failwith "should not happen"