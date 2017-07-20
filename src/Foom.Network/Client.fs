namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let peer = Peer (Udp.Client udpClient)

    member val Connected = peer.PeerConnected

    member this.Connect (address, port) = peer.Connect (address, port)

    member this.Subscribe<'T> f =
        peer.Subscribe<'T> f

    member this.Update time =
        peer.Update time
