namespace Foom.Network

open System.Collections.Generic

type BasicChannelState =
    {
        sharedPacketPool :          PacketPool

        unreliableReceiver :        UnreliableReceiver
        unreliableSender :          UnreliableSender

        reliableOrderedReceiver :   ReliableOrderedReceiver
        reliableOrderedSender :     ReliableOrderedChannel

        reliableOrderedAckSender :  ReliableOrderedAckSender

        sendPacketQueue :           Queue<Packet>
    }

    static member Create (packetPool, receive, send, sendAck) =
        let reliableOrderedAckSender = ReliableOrderedAckSender (packetPool, send)

        let sendAck = fun ack -> sendAck ack reliableOrderedAckSender.Send

        {
            sharedPacketPool = packetPool

            unreliableReceiver = UnreliableReceiver (packetPool, receive)
            unreliableSender = UnreliableSender (packetPool, send)

            reliableOrderedReceiver = ReliableOrderedReceiver (packetPool, sendAck, receive)
            reliableOrderedSender = ReliableOrderedChannel (packetPool, send)

            reliableOrderedAckSender = reliableOrderedAckSender

            sendPacketQueue = new Queue<Packet> ()
        }

    member this.Send (bytes, startIndex, size, packetType) =
        match packetType with
        | PacketType.Unreliable ->
            this.unreliableSender.Send (bytes, startIndex, size)

        | PacketType.ReliableOrdered ->
            this.reliableOrderedSender.Send (bytes, startIndex, size)

        | PacketType.ReliableOrderedAck ->
            this.reliableOrderedAckSender.Send (bytes, startIndex, size)

        | _ -> failwith "packet type not supported"

    member this.Receive (time, packet : Packet) =
        match packet.Type with

        | PacketType.Unreliable ->
            this.unreliableReceiver.Receive (time, packet)
            true

        | PacketType.ReliableOrdered ->
            this.reliableOrderedReceiver.Receive (time, packet)
            true

        | PacketType.ReliableOrderedAck ->
            packet.ReadAcks this.reliableOrderedSender.Ack
            this.sharedPacketPool.Recycle packet
            true

        | _ -> false

    member this.UpdateReceive time =
        this.unreliableReceiver.Update time
        this.reliableOrderedReceiver.Update time

    member this.UpdateSend time =
        this.unreliableSender.Update time
        this.reliableOrderedAckSender.Update time
        this.reliableOrderedSender.Update time