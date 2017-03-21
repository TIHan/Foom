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

    member this.Length = byteStream.Length

    member this.Raw = byteStream.Raw

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
type UnreliableChannel (client: ConnectedClient, endPoint: IUdpEndPoint, udpServer: IUdpServer) =

    let outgoingQueue = Queue<Packet> ()

    member this.EnqueuePacket (packet: Packet) =
        outgoingQueue.Enqueue (packet)

    member this.Process () =
        while outgoingQueue.Count > 0 do
            let packet = outgoingQueue.Dequeue ()

            client.SendPacketNow packet
            client.RecyclePacket packet

and [<Sealed>] ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) as this =

    // Channels
    let unreliableChannel = UnreliableChannel (this, endPoint, udpServer)

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))

    member this.SendData (data, startIndex, size) =
        let packet = packetPool.Pop ()

        packet.SetData (PacketType.Unreliable, data, startIndex, size)

        unreliableChannel.EnqueuePacket packet

    member this.SendPacketNow (packet: Packet) =
        udpServer.Send (packet.Raw, packet.Length, endPoint) |> ignore

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

    member this.Receive () =

        while udpServer.IsDataAvailable do
            recvStream.Length <- 0

            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            match udpServer.Receive (recvStream.Raw, recvStream.Raw.Length, &endPoint) with
            | 0 -> ()
            | byteCount ->

                recvStream.Length <- byteCount

                // read header
                let typ = recvReader.ReadByte ()
                let sequenceId = recvReader.ReadUInt16 ()
                let fragmentCount = recvReader.ReadByte ()

                match LanguagePrimitives.EnumOfValue typ with
                | PacketType.ConnectionRequested ->

                    let client = ConnectedClient (endPoint, udpServer)

                    clients.Add client

                    let packet = Packet ()
                    packet.SetData (PacketType.ConnectionAccepted, [||], 0, 0)

                    client.SendPacketNow (packet)

                | _ -> ()

    member this.Send () =
        clients
        |> Seq.iter (fun client -> client.Update ())

[<Sealed>]
type Client () =

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))

