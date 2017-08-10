namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let peer = new ClientPeer (udpClient)

    [<CLIEvent>]
    member val Connected = peer.Connected

    [<CLIEvent>]
    member val Disconnected = peer.Disconnected

    member this.IsConnected = peer.IsConnected

    member this.Connect (address, port) = 
        if this.IsConnected then
            failwith "Client is already connected."

        peer.Connect (address, port)

    member this.Disconnect () =
        if not this.IsConnected then
            failwith "Client is already disconnected."

        peer.Disconnect ()

    member this.Subscribe<'T> f =
        peer.Subscribe<'T> f

    member this.Update time =
        peer.Update time

    member this.PacketPoolCount = peer.PacketPool.Count

    member this.PacketPoolMaxCount = peer.PacketPool.MaxCount

