namespace Foom.Network

open System
open System.Collections.Generic

type PacketMerger (packetPool : PacketPool) =

    let packets = ResizeArray<Packet> (packetPool.Amount)

    member x.EnqueuePacket (packet : Packet) =
        if packets.Count > 0 then

            let mutable done' = false
            for i = 0 to packets.Count - 1 do
                let packet' = packets.[i]
                let sizeRemaining = packet'.Raw.Length - packet'.Length
                if sizeRemaining > packet.Length && not done' then
                    packet'.Merge packet
                    packetPool.Recycle packet
                    done' <- true

            if not done' then
                packets.Add packet
        else
            packets.Add packet


    member x.Process f =
        packets
        |> Seq.iter (fun packet ->
            f packet
            packetPool.Recycle packet
        )
        packets.Clear ()
