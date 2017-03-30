namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let receiveBuffer = Array.zeroCreate<byte> (NetConstants.PacketSize + sizeof<PacketHeader>)
    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let packetPool = PacketPool 64
    let packetQueue = Queue<Packet> ()

    let mutable isConnected = false
    let connectedEvent = Event<IUdpEndPoint> ()

    // Pipelines
    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let basicReceiverPipeline = basicReceiver receiveByteStream

    // Channels
    let reliableOrderedChannel = ReliableOrderedChannelReceiver (packetPool)

    member val Connected = connectedEvent.Publish

    member this.Connect (address, port) =
        if udpClient.Connect (address, port) then
            let packet = packetPool.Get ()
            packet.SetData ([||], 0, 0)
            packet.PacketType <- PacketType.ConnectionRequested
            packetQueue.Enqueue packet

    member private this.OnReceive (reader : ByteReader) =

        let rec onReceive (reader : ByteReader) =
            let header = reader.Read<PacketHeader> ()

            match header.type' with
            | PacketType.Unreliable ->
                let startingPos = reader.Position
                while int header.size + startingPos > reader.Position do
                    let typeId = reader.ReadByte () |> int

                    if subscriptions.Length > typeId then
                        let pickler = Network.FindTypeById typeId
                        let msg = pickler.ctor reader
                        pickler.deserialize msg reader
                        subscriptions.[typeId].Trigger msg
                    else
                        failwith "This shouldn't happen."

            | PacketType.ReliableOrdered ->

                ()

            | PacketType.ConnectionAccepted ->

                isConnected <- true
                connectedEvent.Trigger (udpClient.RemoteEndPoint)

            | _ -> ()

        while not reader.IsEndOfStream do
            onReceive reader

    member private this.Receive () =
        receiveByteStream.Length <- 0

        while udpClient.IsDataAvailable do

            match udpClient.Receive (receiveBuffer, 0, receiveBuffer.Length) with
            | 0 -> ()
            | byteCount ->
                match LanguagePrimitives.EnumOfValue receiveBuffer.[0] with
                | PacketType.ReliableOrdered -> ()
                | _ -> basicReceiverPipeline.Send (receiveBuffer, 0, byteCount)

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
        this.Receive ()

        basicReceiverPipeline.Process ()

        // Perform OnReceive after processing all incoming packets.
        receiveByteStream.Position <- 0
        this.OnReceive receiverByteReader

        this.Send ()
