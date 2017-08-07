namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Server (udpServer: IUdpServer, compression) =

    let peer = new ServerPeer (udpServer, TimeSpan.FromSeconds 5., compression)

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

    member this.ClientPacketPoolMaxCount = peer.ClientPacketPoolMaxCount

    member this.ClientPacketPoolCount = peer.ClientPacketPoolCount
