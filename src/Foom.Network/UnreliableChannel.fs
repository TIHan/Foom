namespace Foom.Network

open System
open System.Collections.Generic

type UnreliableChannel (packetPool : PacketPool) =

    member this.ProcessData (data, startIndex, size, f) =
        let packet = packetPool.Get ()
        if size > packet.SizeRemaining then
            failwith "Unreliable data is larger than what a new packet can hold. Consider using reliable sequenced."

        packet.SetData (PacketType.Unreliable, data, startIndex, size)
        f packet

[<AutoOpen>]
module ReliableOrderedChannelImpl =

//http://gafferongames.com/networking-for-game-programmers/reliability-and-flow-control/
//bool sequence_more_recent( unsigned int s1, 
//                           unsigned int s2, 
//                           unsigned int max )
//{
//    return 
//        ( s1 > s2 ) && 
//        ( s1 - s2 <= max/2 ) 
//           ||
//        ( s2 > s1 ) && 
//        ( s2 - s1  > max/2 );
//}
    let sequenceMoreRecent (s1 : uint16) (s2 : uint16) =
        (s1 > s2) &&
        (s1 - s2 <= UInt16.MaxValue / 2us)
            ||
        (s2 > s1) &&
        (s2 - s1 > UInt16.MaxValue / 2us)

type ReliableOrderedChannel (packetPool : PacketPool) =

    let timeToDisconnect = TimeSpan.FromSeconds 10.
    let timeToResend = TimeSpan.FromSeconds 1.
    let mutable nextId = 0us
    let mutable oldestId = -1
    let mutable newestId = -1
    let mutable oldestTimeAck = DateTime ()

    let copyPacketPool = PacketPool (64)
    let acks = Array.init 65536 (fun _ -> true)
    let ackTimes = Array.init 65536 (fun _ -> DateTime ())
    let packets = Array.init 65536 (fun _ -> Unchecked.defaultof<Packet>)

    member this.HasPendingAcks = oldestId <> -1 && newestId <> -1

    member this.ProcessData (data, startIndex, size, f) =
        let packet = packetPool.Get ()
        packet.SetData (PacketType.ReliableOrdered, data, startIndex, size)
        packet.SequenceId <- nextId

        let copypacket = copyPacketPool.Get ()
        copypacket.SetData (PacketType.ReliableOrdered, data, startIndex, size)
        copypacket.SequenceId <- nextId

        let id = int nextId
        if not acks.[id] then
            failwith "This should never happened. We waiting too long."

        let dt = DateTime.UtcNow

        acks.[id] <- false
        ackTimes.[id] <- dt
        packets.[id] <- copypacket

        if oldestId = -1 then
            oldestId <- id
            oldestTimeAck <- dt

        if newestId = -1 then
            newestId <- id
        elif sequenceMoreRecent (uint16 id) (uint16 newestId) then
            newestId <- id

        nextId <- nextId + 1us
        f packet

    member this.TryGetNonAckedPacket (id : uint16, f) =
        let dt = DateTime.UtcNow

        if newestId = -1 && oldestId = -1 then
            ()
        else
            if acks.[int id] then
                ()
            elif (dt - ackTimes.[int id]) < timeToResend then
                ()
            else
                let copypacket = packets.[int id]
                let packet = packetPool.Get ()

                Buffer.BlockCopy (copypacket.Raw, 0, packet.Raw, 0, copypacket.Length)
                packet.Length <- copypacket.Length
                f packet


    member this.Ack (id : uint16) =
        let i = int id
        acks.[i] <- true
        ackTimes.[i] <- DateTime ()
        copyPacketPool.Recycle packets.[i]
        packets.[i] <- Unchecked.defaultof<Packet>

        let mutable nextOldestId = -1
        if oldestId = i then

            if newestId > oldestId then
                for j = newestId downto oldestId + 1 do
                    if not acks.[j] then
                        nextOldestId <- j
                    
            elif newestId < oldestId then
                for j = newestId downto 0 do
                    if not acks.[j] then
                        nextOldestId <- j

                for j = 65536 - 1 downto oldestId + 1 do
                    if not acks.[j] then
                        nextOldestId <- j
            else
                newestId <- -1

        if nextOldestId = -1 then
            oldestId <- -1
        else
            oldestId <- nextOldestId
            oldestTimeAck <- ackTimes.[oldestId]
                  
    member this.Update resend =
        let dt = DateTime.UtcNow

        if oldestId <> -1 && (dt - oldestTimeAck) > timeToDisconnect then
            failwith "Client has lagged out."

        if newestId > oldestId then
            for j = newestId downto oldestId do
                this.TryGetNonAckedPacket (uint16 j, resend)
                
        elif newestId < oldestId then
            for j = newestId downto 0 do
                if not acks.[j] then
                    this.TryGetNonAckedPacket (uint16 j, resend)

            for j = 65536 - 1 downto oldestId do
                if not acks.[j] then
                    this.TryGetNonAckedPacket (uint16 j, resend)