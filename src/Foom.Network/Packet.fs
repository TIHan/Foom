namespace Foom.Network

open System
open System.Collections.Generic

type PacketType =

    | Unreliable = 0uy
    | UnreliableSequenced = 1uy

    | ConnectionRequested = 2uy
    | ConnectionAccepted = 3uy

[<Sealed>]
type Packet () =

    let byteStream = ByteStream (NetConstants.PacketSize)
    let byteWriter = ByteWriter (byteStream)
    let byteReader = ByteReader (byteStream)

    member this.Length = byteStream.Length

    member this.Raw = byteStream.Raw

    member this.Type : PacketType =
        let originalPos = byteStream.Position
        byteStream.Position <- 0
        let typ = LanguagePrimitives.EnumOfValue (byteReader.ReadByte ())
        byteStream.Position <- originalPos
        typ

    member this.SetData (typ: PacketType, bytes: byte [], startIndex: int, size: int) =
        byteStream.Length <- 0

        // setup header
        byteWriter.WriteByte (byte typ)
        byteWriter.WriteUInt16 (0us)
        byteWriter.WriteByte (0uy)

        Buffer.BlockCopy (bytes, startIndex, byteStream.Raw, NetConstants.UdpHeaderSize, size)

        byteStream.Length <- byteStream.Length + size

    member this.Reset () =
        byteStream.Length <- 0

[<Sealed>]
type UnreliableChannel (client: ConnectedClient, endPoint: IUdpEndPoint) =

    let outgoingQueue = Queue<Packet> ()

    member this.EnqueuePacket (packet: Packet) =
        outgoingQueue.Enqueue (packet)

    member this.Process () =
        while outgoingQueue.Count > 0 do
            let packet = outgoingQueue.Dequeue ()

            client.SendData (packet.Raw, packet.Length)
            client.RecyclePacket packet

and [<Sealed>] ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) as this =

    // Channels
    let unreliableChannel = UnreliableChannel (this, endPoint)

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))

    member this.SendPacket (packet: Packet) =
        match packet.Type with

        | PacketType.Unreliable ->
            unreliableChannel.EnqueuePacket packet

        | PacketType.ConnectionAccepted ->
            unreliableChannel.EnqueuePacket packet

        | _ -> ()

    member this.SendData (data, size) =
        udpServer.Send (data, size, endPoint) |> ignore

    member this.SendConnectionAccepted () =
        let packet = packetPool.Pop ()
        packet.SetData (PacketType.ConnectionAccepted, [||], 0, 0)
        this.SendPacket (packet)

    member this.RecyclePacket (packet: Packet) =
        packet.Reset ()
        packetPool.Push (packet)

    member this.Update () =
        unreliableChannel.Process ()

[<Sealed>]
type Server (udpServer: IUdpServer) =

    let recvStream = ByteStream (NetConstants.PacketSize)
    let recvReader = ByteReader (recvStream)

    let clients = ResizeArray<ConnectedClient> ()

    member private this.Receive () =

        while udpServer.IsDataAvailable do
            recvStream.Length <- 0

            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            match udpServer.Receive (recvStream.Raw, 0, recvStream.Raw.Length, &endPoint) with
            | 0 -> ()
            | byteCount ->

                recvStream.Length <- byteCount

                let typ = recvReader.ReadByte ()

                match LanguagePrimitives.EnumOfValue typ with
                | PacketType.ConnectionRequested ->

                    let client = ConnectedClient (endPoint, udpServer)

                    clients.Add client

                    client.SendConnectionAccepted ()

                | _ -> ()

    member this.Send () =
        clients
        |> Seq.iter (fun client -> client.Update ())

    member this.Update () =
        this.Receive ()
        this.Send ()

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let recvStream = ByteStream (NetConstants.PacketSize)
    let recvReader = ByteReader (recvStream)

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))
    let packetQueue = Queue<Packet> ()

    let connected = Event<unit> ()

    member val Connected = connected.Publish

    member this.Connect (address, port) =

        if udpClient.Connect (address, port) then
            let packet = packetPool.Pop ()
            packet.SetData (PacketType.ConnectionRequested, [||], 0, 0)
            packetQueue.Enqueue packet

    member private this.Receive () =

        while udpClient.IsDataAvailable do
            recvStream.Length <- 0

            match udpClient.Receive (recvStream.Raw, 0, recvStream.Raw.Length) with
            | 0 -> ()
            | byteCount ->

                recvStream.Length <- byteCount

                let typ = recvReader.ReadByte ()

                match LanguagePrimitives.EnumOfValue typ with
                | PacketType.ConnectionAccepted ->

                    connected.Trigger ()

                | _ -> ()

    member private this.Send () =
        while packetQueue.Count > 0 do

            let packet = packetQueue.Dequeue ()
            udpClient.Send (packet.Raw, packet.Length) |> ignore
            this.Recycle packet

    member this.Update () =
        this.Receive ()
        this.Send ()

    member this.Recycle (packet: Packet) =
        packet.Reset ()
        packetPool.Push packet
