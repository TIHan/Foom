namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Server (udpServer: IUdpServer) =

    let sendStream = ByteStream (1024 * 1024 * 2)
    let sendWriter = ByteWriter (sendStream)

    let clients = ResizeArray<ConnectedClient> ()

    let clientConnected = Event<IUdpEndPoint> ()

    // Packet Pool
    let packetPool = PacketPool 1024

    // Receivers
    let connectionRequestedReceiver = ConnectionRequestedReceiver (packetPool, fun packet endPoint ->
        let client = ConnectedClient (endPoint, udpServer)

        clients.Add client

        client.SendConnectionAccepted ()

        clientConnected.Trigger endPoint
    )

    let receivers =
        [|
            connectionRequestedReceiver
        |]

    let receive time =
        while udpServer.IsDataAvailable do
            let packet = packetPool.Get ()

            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            match udpServer.Receive (packet.Raw, 0, packet.Raw.Length, &endPoint) with
            | 0 -> ()
            | byteCount ->
                packet.Length <- byteCount
                
                match packet.Type with
                | PacketType.ConnectionRequested ->
                    connectionRequestedReceiver.Receive (time, packet, endPoint)
                | _ -> failwith "Packet type not supported."

        receivers
        |> Array.iter (fun receiver -> receiver.Update time)

    let send time =
        clients
        |> Seq.iter (fun client -> client.Update time)

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
                clients.[i].Send (sendStream.Raw, startIndex, length, PacketType.Unreliable)

        | _ -> ()

    member this.Update time =
        receive time
        send time
        this.BytesSentSinceLastUpdate <- udpServer.BytesSentSinceLastCall ()
        sendStream.Length <- 0
