namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Server (udpServer: IUdpServer) =

    let peer = new ServerPeer (udpServer, TimeSpan.FromSeconds 5.)

    [<CLIEvent>]
    member val ClientConnected = peer.ClientConnected

    [<CLIEvent>]
    member val ClientDisconnected = peer.ClientDisconnected

    member val BytesSentSinceLastUpdate = 0 with get, set

    member this.PublishUnreliable<'T> (msg: 'T) =
        peer.SendUnreliable (msg)

    member this.PublishReliableOrdered<'T> (msg : 'T) =
        peer.SendReliableOrdered (msg)

    member this.Update time =
        peer.Update time
        this.BytesSentSinceLastUpdate <- udpServer.BytesSentSinceLastCall ()

    member this.CanForcePacketLoss 
        with get () = udpServer.CanForceDataLoss
        and set value = udpServer.CanForceDataLoss <- value

    member this.PacketPoolCount = peer.PacketPool.Count

    member this.PacketPoolMaxCount = peer.PacketPool.MaxCount

    member this.ClientPacketPoolMaxCount =
        match peer.Udp with
        | Udp.Server server -> server.peerLookup |> Seq.sumBy (fun pair1 -> pair1.Value.packetPool.MaxCount)
        | _ -> failwith "nope"

    member this.ClientPacketPoolCount =
        match peer.Udp with
        | Udp.Server server -> server.peerLookup |> Seq.sumBy (fun pair1 -> pair1.Value.packetPool.Count)
        | _ -> failwith "nope"
