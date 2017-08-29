namespace Foom.Network

open System
open System.IO
open System.Collections.Generic
open System.IO.Compression

[<Sealed>]
type SendStreamState private (bs : ByteStream) =

    member __.Write (msg : 'T, f) =
        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let startIndex = int bs.Position

            let pickler = Network.FindTypeById id
            bs.Writer.WriteByte (byte id)
            pickler.serialize (msg :> obj) bs.Writer

            let size = int bs.Position - startIndex

            f bs.Raw startIndex (int size)

        | _ -> ()

    member __.Reset () =
        bs.SetLength 0L

    static member Create () =
        SendStreamState (new ByteStream (Array.zeroCreate <| 1024 * 1024))

[<Sealed>]
type ReceiveStreamState private (bs : ByteStream) =

    let read (subscriptions : Event<obj> []) =
        let reader = bs.Reader
        let typeId = reader.ReadByte () |> int

        if subscriptions.Length > typeId && typeId >= 0 then
            let pickler = Network.FindTypeById typeId
            let msg = pickler.ctor reader
            pickler.deserialize msg reader
            subscriptions.[typeId].Trigger msg
        else
            failwith "This shouldn't happen."

    member __.Read subscriptions =
        bs.Position <- 0L

        while not bs.Reader.IsEndOfStream do
            read subscriptions

    member __.Write (bytes, offset, count) =
        bs.Write (bytes, offset, count)

    member this.Reset () =
        bs.SetLength 0L

    static member Create () =
        ReceiveStreamState (new ByteStream (Array.zeroCreate <| 1024 * 1024))

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
        peerDisconnected : Event<unit>
        mutable lastReceiveTime : TimeSpan
        mutable isConnected : bool

        basicChannelState : BasicChannelState
    }

module ClientState =

    let create (udpClient : IUdpClient) connectionTimeout =

        let packetPool = PacketPool 1024
        let sendStreamState = SendStreamState.Create ()
        let receiveStreamState = ReceiveStreamState.Create ()

        let basicChannelState =
            BasicChannelState.Create (packetPool,
                (fun packet ->
                    receiveStreamState.Write (packet.Raw, sizeof<PacketHeader>, int packet.DataLength)
                ),
                udpClient.Send
            )

        {
            packetPool = packetPool
            sendStreamState = sendStreamState
            receiveStreamState = receiveStreamState
            subscriptions = createSubscriptions ()
            udpClient = udpClient
            connectionTimeout = connectionTimeout
            peerConnected = Event<IUdpEndPoint> ()
            peerDisconnected = Event<unit> ()
            basicChannelState = basicChannelState
            lastReceiveTime = TimeSpan.Zero
            isConnected = false
        }

    let receive time (packet : Packet) (state : ClientState) =
        match packet.Type with

        | PacketType.ConnectionAccepted ->
            state.isConnected <- true
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
            state.isConnected <- false
            state.peerDisconnected.Trigger ()
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
        let sendStreamState = SendStreamState.Create ()
        let receiveStreamState = ReceiveStreamState.Create ()

        let basicChannelState =
            BasicChannelState.Create (packetPool,
                (fun packet ->
                    receiveStreamState.Write (packet.Raw, sizeof<PacketHeader>, int packet.DataLength)
                ),
                (fun packet -> udpServer.Send (packet, endPoint))
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
        let sendStreamState = SendStreamState.Create ()

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
            state.peerDisconnected.Trigger endPoint
            true

        | PacketType.Pong ->

            match state.peerLookup.TryGetValue endPoint with
            | true, ccState ->

                ccState.pingTime <- time - packet.Reader.Read<TimeSpan> ()
                state.packetPool.Recycle packet
                true
            | _ -> false
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

    member this.Disconnect () =
        match udp with
        | Udp.Client state ->
            use tmp = new Packet ()
            tmp.Type <- PacketType.Disconnect
            state.udpClient.Send tmp
            state.peerDisconnected.Trigger ()
            state.udpClient.Disconnect ()
        | _ -> failwith "Clients can only disconnect."

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
                match packetType with
                | PacketType.Unreliable ->
                    ccState.basicChannelState.SendUnreliable (bytes, startIndex, size)
                | PacketType.ReliableOrdered ->
                    ccState.basicChannelState.SendReliableOrdered (bytes, startIndex, size)
                | _ -> failwith "bad packet type"
            )

        | Udp.Client state ->
            match packetType with
            | PacketType.Unreliable ->
                state.basicChannelState.SendUnreliable (bytes, startIndex, size)
            | PacketType.ReliableOrdered ->
                state.basicChannelState.SendReliableOrdered (bytes, startIndex, size)
            | _ -> failwith "bad packet type"

    member this.Send<'T> (msg : 'T, packetType) =
        let sendStreamState =
            match udp with
            | Udp.Client state -> state.sendStreamState
            | Udp.Server state -> state.sendStreamState

        sendStreamState.Write (msg, fun bytes startIndex size -> 
            this.Send (bytes, startIndex, size, packetType)
        )
        
     member this.ServerSend (bytes, startIndex, size, packetType, endPoint : IUdpEndPoint) =
        match udp with
        | Udp.Server state ->
            match state.peerLookup.TryGetValue (endPoint) with
            | true, ccState ->
                match packetType with
                | PacketType.Unreliable ->
                    ccState.basicChannelState.SendUnreliable (bytes, startIndex, size)
                | PacketType.ReliableOrdered ->
                    ccState.basicChannelState.SendReliableOrdered (bytes, startIndex, size)
                | _ -> failwith "bad packet type"
            | _ -> ()
        | _ -> ()

    member this.ServerSend<'T> (msg : 'T, packetType, endPoint) =
        match udp with
        | Udp.Server state ->
            state.sendStreamState.Write (msg, fun bytes startIndex size ->
                this.ServerSend (bytes, startIndex, size, packetType, endPoint)
            )
        | _ -> ()

    member this.SendUnreliable<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.Unreliable)

    member this.SendUnreliable<'T> (msg : 'T, endPoint) =
        this.ServerSend<'T> (msg, PacketType.Unreliable, endPoint)

    member this.SendReliableOrdered<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.ReliableOrdered)

    member this.SendReliableOrdered<'T> (msg : 'T, endPoint) =
        this.ServerSend<'T> (msg, PacketType.ReliableOrdered, endPoint)

    member this.ReceivePacket (time, packet : Packet, endPoint : IUdpEndPoint) =
        match udp with
        | Udp.Client state -> 
            state.lastReceiveTime <- time

            match state.basicChannelState.Receive packet with
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

                match ServerState.receive time packet endPoint state with
                | false ->
                    // Trade packets from server and client
                    ccState.packetPool.Get ()
                    |> state.packetPool.Recycle

                    match ccState.basicChannelState.Receive packet with
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

            state.receiveStreamState.Reset ()

            let client = state.udpClient
            let packetPool = state.packetPool

            while client.IsDataAvailable do
                let packet = packetPool.Get ()
                let byteCount = client.Receive (packet)
                if byteCount > 0 then
                    this.ReceivePacket (time, packet, client.RemoteEndPoint)
                else
                    packetPool.Recycle packet

            state.basicChannelState.Update time

            state.receiveStreamState.Read state.subscriptions

            state.sendStreamState.Reset ()

            if time > state.lastReceiveTime + state.connectionTimeout && state.isConnected then
                state.isConnected <- false
                state.peerDisconnected.Trigger ()

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

                ccState.basicChannelState.Update time

                ccState.receiveStreamState.Read state.subscriptions
                ccState.sendStreamState.Reset ()
            )

            state.sendStreamState.Reset ()

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

    member this.IsConnected =
        match this.Udp with
        | Udp.Client state -> state.isConnected
        | _ -> failwith "should not happen"

    member this.Connected =
        match this.Udp with
        | Udp.Client state -> state.peerConnected.Publish
        | _ -> failwith "should not happen"

    member this.Disconnected =
        match this.Udp with
        | Udp.Client state -> state.peerDisconnected.Publish
        | _ -> failwith "should not happen"