namespace Foom.Network

open System
open System.Collections.Generic

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
