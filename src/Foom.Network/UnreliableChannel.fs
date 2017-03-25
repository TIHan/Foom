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

    let mutable nextId = 0us
    let mutable oldestId = -1
    let mutable newestId = -1

    let copyPacketPool = PacketPool (64)
    let acks = Array.init 65536 (fun _ -> true)
    let ackTimes = Array.init 65536 (fun _ -> DateTime ())
    let packets = Array.init 65536 (fun _ -> Unchecked.defaultof<Packet>)

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

        acks.[id] <- false
        ackTimes.[id] <- DateTime.UtcNow
        packets.[id] <- copypacket

        if oldestId = -1 then
            oldestId <- id

        if newestId = -1 then
            newestId <- id
        elif sequenceMoreRecent (uint16 id) (uint16 newestId) then
            newestId <- id

        nextId <- nextId + 1us
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

        oldestId <- nextOldestId
                  