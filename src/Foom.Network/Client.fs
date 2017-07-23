namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let packetPool = PacketPool 1024
    let peer = Peer (Udp.Client udpClient, packetPool)

    member val Connected = peer.PeerConnected

    member this.Connect (address, port) = peer.Connect (address, port)

    member this.Subscribe<'T> f =
        peer.Subscribe<'T> f

    member this.Update time =
        peer.Update time

    member this.PacketPoolCount = packetPool.Count

    member this.PacketPoolMaxCount = packetPool.MaxCount

