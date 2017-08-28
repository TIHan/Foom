namespace Foom.Network

open System.Collections.Generic

type BasicChannelState =
    {
        sharedPacketPool :          PacketPool

        unreliableReceiver :        Receiver
        unreliableSender :          Sender

        reliableOrderedReceiver :   ReliableOrderedReceiver
        reliableOrderedSender :     SenderAck

        reliableOrderedAckSender :  Sender

        send :                      Packet -> unit
        receive :                   Packet -> unit
    }

    static member Create (packetPool, receive, send, sendAck) =
        let reliableOrderedAckSender = Sender.CreateReliableOrderedAck packetPool

        let sendAck = fun ack -> sendAck ack reliableOrderedAckSender.Enqueue

        {
            sharedPacketPool = packetPool

            unreliableReceiver = Receiver.CreateUnreliable packetPool
            unreliableSender = Sender.CreateUnreliable packetPool

            reliableOrderedReceiver = ReliableOrderedReceiver (packetPool, sendAck, receive)
            reliableOrderedSender = SenderAck.CreateReliableOrdered packetPool

            reliableOrderedAckSender = reliableOrderedAckSender

            send = send
            receive = receive
        }

    member this.Send (bytes, startIndex, size, packetType) =
        match packetType with
        | PacketType.Unreliable ->
            this.unreliableSender.Enqueue (bytes, startIndex, size)

        | PacketType.ReliableOrdered ->
            this.reliableOrderedSender.Enqueue (bytes, startIndex, size)

        | PacketType.ReliableOrderedAck ->
            this.reliableOrderedAckSender.Enqueue (bytes, startIndex, size)

        | _ -> failwith "packet type not supported"

    member this.Receive (time, packet : Packet) =
        match packet.Type with

        | PacketType.Unreliable ->
            this.unreliableReceiver.Enqueue packet
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
        this.unreliableReceiver.Flush time
        this.reliableOrderedReceiver.Update time

        this.unreliableReceiver.Process this.receive

    member this.UpdateSend time =
        this.unreliableSender.Flush time
        this.reliableOrderedAckSender.Flush time
        this.reliableOrderedSender.Flush time

        this.unreliableSender.Process this.send
        this.reliableOrderedAckSender.Process this.send
        this.reliableOrderedSender.Process this.send
