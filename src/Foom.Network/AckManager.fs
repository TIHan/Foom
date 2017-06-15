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
type AckManager (ackRetryTime : TimeSpan) =

    let copyPacketPool = PacketPool (64)
    let copyPackets = Array.init 65536 (fun _ -> Unchecked.defaultof<Packet>)
    let acks = Array.init 65536 (fun _ -> true)
    let ackTimes = Array.init 65536 (fun _ -> TimeSpan.Zero)

    let mutable newestAck = -1
    let mutable oldestAck = -1

    member x.ForEachPending time f1 =
        let inline f i packet =
            if time > ackTimes.[i] + ackRetryTime then
                ackTimes.[i] <- ackTimes.[i] + ackRetryTime
                f1 i packet

        if oldestAck <> -1 then

            if newestAck = oldestAck then
                if not acks.[oldestAck] then
                    f oldestAck copyPackets.[oldestAck]
            
            elif newestAck > oldestAck then
                for i = oldestAck to newestAck do
                    if not acks.[i] then
                        f i copyPackets.[i]

            elif newestAck < oldestAck then
                for i = oldestAck to acks.Length - 1 do
                    if not acks.[i] then
                        f i copyPackets.[i]

                for i = 0 to newestAck do
                    if not acks.[i] then
                        f i copyPackets.[i]

    member x.Ack i =
        if not acks.[i] then
            copyPacketPool.Recycle copyPackets.[i]
            copyPackets.[i] <- Unchecked.defaultof<Packet>

            acks.[i] <- true
            ackTimes.[i] <- TimeSpan.Zero

            if oldestAck = newestAck && oldestAck = i then
                oldestAck <- -1
                newestAck <- -1

            elif oldestAck = i then
                let mutable n = uint16 oldestAck
                let mutable nextOldestAck = -1
                while nextOldestAck = -1 do
                    n <- n + 1us
                    if not acks.[int n] then
                        nextOldestAck <- int n
                oldestAck <- nextOldestAck

            elif newestAck = i then
                let mutable n = uint16 oldestAck
                let mutable nextNewestAck = -1
                while nextNewestAck = -1 do
                    n <- n - 1us
                    if not acks.[int n] then
                        nextNewestAck <- int n
                newestAck <- nextNewestAck

    member x.MarkCopy (packet : Packet, time) =
        let i = int packet.SequenceId
        if acks.[i] then
            let packet' = copyPacketPool.Get ()
            packet.CopyTo packet' 
            copyPackets.[i] <- packet'

            acks.[i] <- false
            ackTimes.[i] <- time

            if oldestAck = -1 then
                oldestAck <- i
            elif sequenceMoreRecent (uint16 oldestAck) (uint16 i) then
                oldestAck <- i

            if newestAck = -1 then
                newestAck <- i
            elif sequenceMoreRecent (uint16 i) (uint16 newestAck) then
                newestAck <- i
