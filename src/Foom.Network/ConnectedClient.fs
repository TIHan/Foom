namespace Foom.Network

open System
open System.Collections.Generic

type DataFlow (input, output) =

    let sendStream = ByteStream (1024 * 1024)
    let sendWriter = ByteWriter (sendStream)

    let receiveByteStream = ByteStream (1024 * 1024)
    let receiverByteReader = ByteReader (receiveByteStream)
    let receiverByteWriter = ByteWriter (receiveByteStream)

    let packetPool = PacketPool 1024

    let sender = Sender.create packetPool input
    let receiver = Receiver.create packetPool (fun ack -> sender.Send { bytes = [||]; startIndex = 0; size = 0; packetType = PacketType.ReliableOrderedAck; ack = int ack }) output

    

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
