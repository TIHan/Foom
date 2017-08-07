namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient, compression) =

    let peer = new ClientPeer (udpClient, compression)

    [<CLIEvent>]
    member val Connected = peer.Connected

    [<CLIEvent>]
    member val Disconnected = peer.Disconnected

    member this.Connect (address, port) = peer.Connect (address, port)

    member this.Subscribe<'T> f =
        peer.Subscribe<'T> f

    member this.Update time =
        peer.Update time

    member this.PacketPoolCount = peer.PacketPool.Count

    member this.PacketPoolMaxCount = peer.PacketPool.MaxCount

