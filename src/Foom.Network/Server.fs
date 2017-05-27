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
        let startIndex = sendStream.Position

        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let pickler = Network.FindTypeById id
            sendWriter.WriteByte (byte id)
            pickler.serialize (msg :> obj) sendWriter

            let length = sendStream.Position - startIndex

            for i = 0 to clients.Count - 1 do
                clients.[i].Send (sendStream.Raw, startIndex, length)

        | _ -> ()

    member this.Update () =
        sendStream.Length <- 0
        receive ()
        send ()
        this.BytesSentSinceLastUpdate <- udpServer.BytesSentSinceLastCall ()
