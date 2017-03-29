namespace Foom.Network

open System
open System.Collections.Generic

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
[<AutoOpen>]
module AcksInternal =

    let sequenceMoreRecent (s1 : uint16) (s2 : uint16) =
        (s1 > s2) &&
        (s1 - s2 <= UInt16.MaxValue / 2us)
            ||
        (s2 > s1) &&
        (s2 - s1 > UInt16.MaxValue / 2us)

[<Sealed>]
type AckManager () =

    let copyPacketPool = PacketPool (64)
    let copyPackets = Array.init 65536 (fun _ -> Unchecked.defaultof<Packet>)
    let mutable acks = Array.init 65536 (fun _ -> true)
    let mutable ackTimes = Array.init 65536 (fun _ -> DateTime ())

    let mutable newestAck = -1
    let mutable oldestAck = -1
    let mutable oldestTimeAck = DateTime ()

    let pending = Queue ()

    member x.ForEachPending f =
        if newestAck = oldestAck && newestAck <> -1 then
            if not acks.[oldestAck] then
                f oldestAck ackTimes.[oldestAck] copyPackets.[oldestAck]
        
        elif newestAck > oldestAck then
            for i = oldestAck to newestAck do
                if not acks.[i] then
                    f i ackTimes.[i] copyPackets.[i]

        elif oldestAck < newestAck then
            for i = oldestAck to acks.Length - 1 do
                if not acks.[i] then
                    f i ackTimes.[i] copyPackets.[i]

            for i = 0 to newestAck do
                if not acks.[i] then
                    f i ackTimes.[i] copyPackets.[i]

    member x.Ack i =
        if not acks.[i] then
            copyPacketPool.Recycle copyPackets.[i]
            copyPackets.[i] <- Unchecked.defaultof<Packet>

            acks.[i] <- true
            ackTimes.[i] <- DateTime ()

            if oldestAck = i then
                oldestAck <- -1
                oldestTimeAck <- DateTime ()

            while oldestAck = -1 && pending.Count > 0 do
                let j = pending.Dequeue ()
                if not acks.[j] then
                    oldestAck <- j
                    oldestTimeAck <- ackTimes.[j]

    member x.MarkCopy (packet : Packet) =
        let i = int packet.SequenceId
        if acks.[i] then
            let dt = DateTime.UtcNow
            let packet' = copyPacketPool.Get ()
            packet.CopyTo packet' 
            copyPackets.[i] <- packet'

            acks.[i] <- true
            ackTimes.[i] <- DateTime.UtcNow

            if oldestAck = -1 then
                oldestAck <- i
                oldestTimeAck <- DateTime.UtcNow

            if newestAck = -1 then
                newestAck <- i
            elif sequenceMoreRecent (uint16 i) (uint16 newestAck) then
                newestAck <- i

            pending.Enqueue i
