namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type DataFlow (send) =

    let packetPool = PacketPool 1024

    let subscriptions = Array.init 1024 (fun _ -> Event<obj> ())

    let sendStream = ByteStream (1024 * 1024)
    let sendWriter = ByteWriter (sendStream)

    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let receiverByteWriter = ByteWriter (receiveByteStream)

    let sender = 
        Sender.create packetPool (fun packet ->
            send packet
        )

    let receiver =
        Receiver.create sender packetPool (fun packet ->
            receiverByteWriter.WriteRawBytes (packet.Raw, sizeof<PacketHeader>, packet.DataLength)
        )

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

    member private this.Send<'T> (msg : 'T) packetType =
        let startIndex = sendStream.Position

        match Network.lookup.TryGetValue typeof<'T> with
        | true, id ->
            let pickler = Network.FindTypeById id
            sendWriter.WriteByte (byte id)
            pickler.serialize (msg :> obj) sendWriter

            let length = sendStream.Position - startIndex

            sender.Send { bytes = sendStream.Raw; startIndex = startIndex; size = length; packetType = packetType; ack = 0 }

        | _ -> ()

    member this.SendUnreliable<'T> (msg : 'T) =
        this.Send<'T> msg PacketType.Unreliable

    member this.SendReliableOrdered<'T> (msg : 'T) =
        this.Send<'T> msg PacketType.ReliableOrdered

    member this.Update time =
        receiveByteStream.Length <- 0
        receiver.Process time
        receiveByteStream.Position <- 0

        this.OnReceive receiverByteReader

        sender.Process time

[<Sealed>]
type ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) as this =

    let packetPool = PacketPool 1024

    let packetQueue = Queue<Packet> ()

    // Pipelines

    // Senders
    let senderUnreliable = 
        Sender.createUnreliable packetPool (fun packet -> 
            this.SendNow (packet.Raw, packet.Length)
        )

    let senderReliableOrdered =
        Sender.createReliableOrdered packetPool (fun packet ->
            this.SendNow (packet.Raw, packet.Length)
        )

    member this.SendNow (data : byte [], size) =
        if size > 0 && data.Length > 0 then
            udpServer.Send (data, size, endPoint) |> ignore

    member this.Send (data, startIndex, size) =
        senderUnreliable.Send { bytes = data; startIndex = startIndex; size = size; packetType = PacketType.Unreliable; ack = 0 }

    member this.SendConnectionAccepted () =
        let packet = Packet ()
        packet.Type <- PacketType.ConnectionAccepted

        packetQueue.Enqueue packet

    member this.Update time =

        while packetQueue.Count > 0 do
            let packet = packetQueue.Dequeue ()
            this.SendNow (packet.Raw, packet.Length)

        senderUnreliable.Process time
