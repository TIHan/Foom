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

module BasicChannelState =

    let create packetPool receive send sendAck =
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

    let send bytes startIndex size packetType state =
        match packetType with
        | PacketType.Unreliable ->
            state.unreliableSender.Send (bytes, startIndex, size)

        | PacketType.ReliableOrdered ->
            state.reliableOrderedSender.Send (bytes, startIndex, size)

        | PacketType.ReliableOrderedAck ->
            state.reliableOrderedAckSender.Send (bytes, startIndex, size)

        | _ -> failwith "packet type not supported"

    let receive time (packet : Packet) state =
        match packet.Type with

        | PacketType.Unreliable ->
            state.unreliableReceiver.Receive (time, packet)
            true

        | PacketType.ReliableOrdered ->
            state.reliableOrderedReceiver.Receive (time, packet)
            true

        | PacketType.ReliableOrderedAck ->
            packet.ReadAcks state.reliableOrderedSender.Ack
            state.sharedPacketPool.Recycle packet
            true

        | _ -> false

type BasicChannelState with

    member this.UpdateReceive time =
        this.unreliableReceiver.Update time
        this.reliableOrderedReceiver.Update time

    member this.UpdateSend time =
        this.unreliableSender.Update time
        this.reliableOrderedAckSender.Update time
        this.reliableOrderedSender.Update time