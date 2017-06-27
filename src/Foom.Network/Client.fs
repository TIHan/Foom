namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) as this =

    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let packetPool = PacketPool 2048
    let packetQueue = Queue<Packet> ()

    let mutable isConnected = false
    let connectedEvent = Event<IUdpEndPoint> ()

    let sendStream = ByteStream (1024 * 1024)
    let sendWriter = ByteWriter (sendStream)

    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let receiverByteWriter = ByteWriter (receiveByteStream)

    // Pipelines

    // Senders
    let senderUnreliable =
        Sender.createUnreliable packetPool (fun packet ->
            this.SendNow (packet.Raw, packet.Length)
        )

    // Receivers
    let receiverUnreliable = 
        Receiver.createUnreliable packetPool (fun packet ->
            receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.DataLength)
        )

    //let receiverReliableOrdered =
        //Receiver.createReliableOrdered packetPool (fun packet ->
        //    receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.DataLength)
        //)

    member this.SendNow (data : byte [], size) =
        if size > 0 && data.Length > 0 then
            udpClient.Send (data, size) |> ignore

    member val Connected = connectedEvent.Publish

    member this.Connect (address, port) =
        if udpClient.Connect (address, port) then
            let packet = packetPool.Get ()
            packet.Type <- PacketType.ConnectionRequested
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

    member private this.Receive time =

        while udpClient.IsDataAvailable do
            let packet = packetPool.Get ()
            match udpClient.Receive (packet.Raw, 0, packet.Raw.Length) with
            | 0 -> ()
            | byteCount ->
                packet.Length <- byteCount
                match packet.Type with

                | PacketType.ConnectionAccepted ->
                    isConnected <- true
                    connectedEvent.Trigger (udpClient.RemoteEndPoint)

                | PacketType.Unreliable -> receiverUnreliable.Send (packet)

               // | PacketType.ReliableOrdered -> receiverReliableOrdered.

                | _ -> failwithf "Unsupported packet type: %A" packet.Type

        receiverUnreliable.Process time

    member private this.Send time =
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

    member this.Update time =
        receiveByteStream.Length <- 0

        this.Receive time

        // Perform OnReceive after processing all incoming packets.
        receiveByteStream.Position <- 0
        this.OnReceive receiverByteReader

        this.Send ()
