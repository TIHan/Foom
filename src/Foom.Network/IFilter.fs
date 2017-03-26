namespace Foom.Network

open System
open System.Collections.Generic

type Source (packetPool : PacketPool, map: byte[] -> int -> int -> Packet -> unit) =

    let queue = Queue<Packet> ()

    member this.SendData (data, startIndex, size) =
        let packet = packetPool.Get ()

        map data startIndex size packet

        queue.Enqueue packet
 
    member this.Flush f =
        while queue.Count > 0 do
            let packet = queue.Dequeue ()
            f packet
            packetPool.Recycle packet


type SequenceFilter (packetPool : PacketPool) =

    let mutable nextSeqId = 0us

    member this.In (packet : Packet) =
        match packet.PacketType with
        | PacketType.UnreliableSequenced
        | PacketType.Reliable
        | PacketType.ReliableSequenced
        | PacketType.ReliableOrdered ->

            ()

        | _ -> failwith "Can't send packet unreliable packet with a sequence id."
