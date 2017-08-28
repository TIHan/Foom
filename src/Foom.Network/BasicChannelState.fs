namespace Foom.Network

open System.Collections.Generic

type BasicChannelState =
    {
        sharedPacketPool :          PacketPool

        unreliableReceiver :        Receiver
        unreliableSender :          Sender

        reliableOrderedReceiver :   ReceiverAck
        reliableOrderedSender :     SenderAck

        reliableOrderedAckSender :  Sender

        send :                      Packet -> unit
        sendAck :                   uint16 -> unit
        receive :                   Packet -> unit
    }

    static member Create (packetPool, receive, send, sendAck) =
        let reliableOrderedAckSender = Sender.CreateReliableOrderedAck packetPool

        let sendAck = fun ack -> sendAck ack reliableOrderedAckSender.Enqueue

        {
            sharedPacketPool = packetPool

            unreliableReceiver = Receiver.CreateUnreliable packetPool
            unreliableSender = Sender.CreateUnreliable packetPool

            reliableOrderedReceiver = ReceiverAck.CreateReliableOrdered packetPool
            reliableOrderedSender = SenderAck.CreateReliableOrdered packetPool

            reliableOrderedAckSender = reliableOrderedAckSender

            send = send
            sendAck = sendAck
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

    member this.Receive (packet : Packet) =
        match packet.Type with

        | PacketType.Unreliable ->
            this.unreliableReceiver.Enqueue packet
            true

        | PacketType.ReliableOrdered ->
            this.reliableOrderedReceiver.Enqueue packet
            true

        | PacketType.ReliableOrderedAck ->
            packet.ReadAcks this.reliableOrderedSender.Ack
            this.sharedPacketPool.Recycle packet
            true

        | _ -> false

    member this.UpdateReceive time =
        this.unreliableReceiver.Flush time
        this.reliableOrderedReceiver.Flush time

        this.unreliableReceiver.Process this.receive
        this.reliableOrderedReceiver.Process (fun packet ->
            this.sendAck packet.SequenceId
            this.receive packet
        )

    member this.UpdateSend time =
        this.unreliableSender.Flush time
        this.reliableOrderedAckSender.Flush time
        this.reliableOrderedSender.Flush time

        this.unreliableSender.Process this.send
        this.reliableOrderedAckSender.Process this.send
        this.reliableOrderedSender.Process this.send
