namespace Foom.Network

open System
open System.Collections.Generic

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

    member this.Send (bytes, startIndex, size, packetType) =
        sender.Send { bytes = bytes; startIndex = startIndex; size = size; packetType = packetType; ack = 0 }

    member private this.Send<'T> (msg : 'T, packetType) =
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
        this.Send<'T> (msg, PacketType.Unreliable)

    member this.SendReliableOrdered<'T> (msg : 'T) =
        this.Send<'T> (msg, PacketType.ReliableOrdered)

    member this.Update (time, receive) =
        receiveByteStream.Length <- 0

        while receive packetPool receiver.Send do ()

        receiver.Process time
        receiveByteStream.Position <- 0

        this.OnReceive receiverByteReader

        sender.Process time

[<Sealed>]
type ConnectedClient (endPoint: IUdpEndPoint, udpServer: IUdpServer) =

    let flow = DataFlow (fun packet -> udpServer.Send (packet.Raw, packet.Length, endPoint) |> ignore)

    member this.SendConnectionAccepted () =
        let packet = Packet ()
        packet.Type <- PacketType.ConnectionAccepted

        udpServer.Send (packet.Raw, packet.Length, endPoint) |> ignore

    member this.Send (bytes, startIndex, size, packetType) =
        flow.Send (bytes, startIndex, size, packetType)

    member this.Update time =
        flow.Update (time, fun _ _ -> false)
        
