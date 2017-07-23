namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Server (udpServer: IUdpServer) =

    let packetPool = PacketPool 1024
    let peer = Peer (Udp.Server udpServer, packetPool)

    [<CLIEvent>]
    member val ClientConnected = peer.PeerConnected

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

    member this.PacketPoolCount = packetPool.Count

    member this.PacketPoolMaxCount = packetPool.MaxCount
