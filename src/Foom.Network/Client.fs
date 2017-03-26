namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let packetPool = PacketPool (64)
    let packetQueue = Queue<Packet> ()

    let mutable isConnected = false
    let connected = Event<IUdpEndPoint> ()

    // Channels
    let reliableOrderedChannel = ReliableOrderedChannelReceiver (packetPool)

    member val Connected = connected.Publish

    member this.Connect (address, port) =

        if udpClient.Connect (address, port) then
            let packet = packetPool.Get ()
            packet.SetData (PacketType.ConnectionRequested, [||], 0, 0)
            packetQueue.Enqueue packet

    member private this.OnReceivePacket (packet: Packet) =
        let reader = packet.Reader

        let rec onReceivePacket (reader : ByteReader) =
            let header = reader.Read<PacketHeader> ()

            match header.type' with
            | PacketType.Unreliable ->

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
                connected.Trigger (udpClient.RemoteEndPoint)

            | _ -> ()

            while not reader.IsEndOfStream do
                onReceivePacket reader

        onReceivePacket reader

    member private this.Receive () =

        while udpClient.IsDataAvailable do
            let packet = packetPool.Get ()

            match udpClient.Receive (packet.Raw, 0, packet.Raw.Length) with
            | 0 -> ()
            | byteCount ->
                packet.Length <- byteCount
                this.OnReceivePacket packet

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
        this.Send ()
