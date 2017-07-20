namespace Foom.Network

open System
open System.Collections.Generic

[<RequireQualifiedAccess>]
type Udp =
    | Client of IUdpClient
    | Server of IUdpServer
    | ServerWithEndPoint of IUdpServer * IUdpEndPoint

type Peer (udp : Udp) =

    let packetPool = PacketPool 1024

    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let sendStream = ByteStream (1024 * 1024)
    let sendWriter = ByteWriter (sendStream)

    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let receiverByteWriter = ByteWriter (receiveByteStream)

    let send = fun bytes size -> 
        match udp with
        | Udp.Client client -> client.Send (bytes, size) |> ignore
        | Udp.ServerWithEndPoint (server, endPoint) -> server.Send (bytes, size, endPoint) |> ignore
        | _ -> failwith "should not happen"

    let unreliableChan = UnreliableChannel (packetPool, send)
    let reliableOrderedChan = ReliableOrderedChannel (packetPool, send)

    let peerLookup = Dictionary<IUdpEndPoint, Peer> ()

    let peerConnected = Event<IUdpEndPoint> ()


    // Receivers
    let connectionRequestedReceiver = ConnectionRequestedReceiver (packetPool, fun packet endPoint ->
        match udp with
        | Udp.Server udpServer ->
            let peer = Peer (Udp.ServerWithEndPoint (udpServer, endPoint))

            peerLookup.Add (endPoint, peer)
            peerConnected.Trigger endPoint
        | _ -> ()
    )

    let receivers =
        [|
            connectionRequestedReceiver
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
            unreliableChan.Send (bytes, startIndex, size)

    member private this.Send<'T> (msg : 'T, packetType) =
        let startIndex = sendStream.Position

        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let pickler = Network.FindTypeById id
            sendWriter.WriteByte (byte id)
            pickler.serialize (msg :> obj) sendWriter

            let size = sendStream.Position - startIndex

            unreliableChan.Send (sendStream.Raw, startIndex, size)

        | _ -> ()

    member this.SendUnreliable<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.Unreliable)

    member this.SendReliableOrdered<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.ReliableOrdered)

    member this.ReceivePacket (packet : Packet) =
        receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.DataLength)

    member this.ReceivePacket (packet : Packet, endPoint : IUdpEndPoint) =
        receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.DataLength)

    member this.Update time =
        receiveByteStream.Length <- 0

        match udp with
        | Udp.Client client ->
            if client.IsDataAvailable then
                let packet = packetPool.Get ()
                let byteCount = client.Receive (packet.Raw, 0, packet.Raw.Length)
                if byteCount > 0 then
                    packet.Length <- byteCount
                    this.ReceivePacket packet
                else
                    packetPool.Recycle packet
        | Udp.Server server ->

            if server.IsDataAvailable then
                let packet = packetPool.Get ()
                let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
                let byteCount = server.Receive (packet.Raw, 0, packet.Raw.Length, &endPoint)
                if byteCount > 0 then
                    packet.Length <- byteCount
                    this.ReceivePacket (packet, endPoint)
                else
                    packetPool.Recycle packet

        | Udp.ServerWithEndPoint _ -> ()

        receiveByteStream.Position <- 0

        receivers
        |> Array.iter (fun receiver -> receiver.Update time)

        this.OnReceive receiverByteReader

        unreliableChan.Update time

[<Sealed>]
type ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) =

    let peer = Peer (Udp.ServerWithEndPoint (udpServer, endPoint))

    member this.SendConnectionAccepted () =
        let packet = Packet ()
        packet.Type <- PacketType.ConnectionAccepted

        udpServer.Send (packet.Raw, packet.Length, endPoint) |> ignore

    member this.Send (bytes, startIndex, size, packetType) =
        peer.Send (bytes, startIndex, size, packetType)

    member this.Update time =
        peer.Update time
        
