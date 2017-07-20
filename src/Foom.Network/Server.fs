namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Server (udpServer: IUdpServer) =

    let peer = Peer (Udp.Server udpServer)

    [<CLIEvent>]
    member val ClientConnected = peer.PeerConnected

    member val BytesSentSinceLastUpdate = 0 with get, set

    member this.PublishUnreliable<'T> (msg: 'T) =
        peer.SendUnreliable (msg)

    member this.Update time =
        peer.Update time
        this.BytesSentSinceLastUpdate <- udpServer.BytesSentSinceLastCall ()
