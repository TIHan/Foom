namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let packetPool = PacketPool 2048
    let packetQueue = Queue<Packet> ()

    let mutable isConnected = false
    let connectedEvent = Event<IUdpEndPoint> ()

    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let receiverByteWriter = ByteWriter (receiveByteStream)

    // Pipelines

    // Receiver
    let receiverUnreliable = Receiver.createUnreliable ()

    do
        receiverUnreliable.Output.Add (fun packet ->
            receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.Size)
            packetPool.Recycle packet
        )

    member val Connected = connectedEvent.Publish

    member this.Connect (address, port) =
        if udpClient.Connect (address, port) then
            let packet = packetPool.Get ()
            packet.PacketType <- PacketType.ConnectionRequested
            packetQueue.Enqueue packet

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

    member private this.Receive () =

        while udpClient.IsDataAvailable do
            let packet = packetPool.Get ()
            match udpClient.Receive (packet.Raw, 0, packet.Raw.Length) with
            | 0 -> ()
            | byteCount ->
                packet.Length <- byteCount
                match packet.PacketType with

                | PacketType.ConnectionAccepted ->
                    isConnected <- true
                    connectedEvent.Trigger (udpClient.RemoteEndPoint)

                | PacketType.Unreliable -> receiverUnreliable.Send (packet)

                | _ -> failwithf "Unsupported packet type: %A" packet.PacketType

        receiverUnreliable.Process ()

    member private this.Send () =
        while packetQueue.Count > 0 do

            let packet = packetQueue.Dequeue ()
            udpClient.Send (packet.Raw, packet.Length) |> ignore
            packetPool.Recycle packet

    member this.Subscribe<'T> f =
        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let evt = subscriptions.[id]
            let pickler = Network.FindTypeById id

            evt.Publish.Add (fun msg -> f (msg :?> 'T))
        | _ -> ()

    member this.Update () =
        receiveByteStream.Length <- 0

        this.Receive ()

        // Perform OnReceive after processing all incoming packets.
        receiveByteStream.Position <- 0
        this.OnReceive receiverByteReader

        this.Send ()
