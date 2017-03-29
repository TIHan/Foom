namespace Foom.Network

open System

type PacketMerger (packetPool : PacketPool) =

     let packets = ResizeArray<Packet> (packetPool.Amount)

     interface IFilter with

        member x.Send (packet : Packet) =
            if packets.Count > 0 then

                let mutable done' = false
                for i = 0 to packets.Count - 1 do
                    let packet' = packets.[i]
                    if packet'.LengthRemaining > packet.Length && not done' then
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

            packetPool.Recycle packet


        member x.Process output =
            packets
            |> Seq.iter (fun packet ->
                output packet
            )
            packets.Clear ()
