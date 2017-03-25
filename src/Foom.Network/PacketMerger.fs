namespace Foom.Network

open System
open System.Collections.Generic

type PacketMerger (packetPool : PacketPool) =

    let packets = ResizeArray<Packet> (packetPool.Amount)

    member x.SendPacket (packet : Packet) =
        if packets.Count > 0 then

            let mutable done' = false
            for i = 0 to packets.Count - 1 do
                let packet' = packets.[i]
                if packet'.SizeRemaining > packet.Length && not done' then
                    packet'.Merge packet
                    done' <- true

            if not done' then
                let packet' = packetPool.Get ()
                packet.CopyTo packet'
                packets.Add packet'
        else
            let packet' = packetPool.Get ()
            packet.CopyTo packet'
            packets.Add packet'


    member x.Flush f =
        packets
        |> Seq.iter (fun packet ->
            f packet
            packetPool.Recycle packet
        )
        packets.Clear ()
