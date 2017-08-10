namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) as this =

    let peer = new ClientPeer (udpClient)

    do
        peer.Connected.Add (fun _ -> this.IsConnected <- true)
        peer.Disconnected.Add (fun _ -> this.IsConnected <- false)

    [<CLIEvent>]
    member val Connected = peer.Connected

    [<CLIEvent>]
    member val Disconnected = peer.Disconnected

    member val IsConnected = false with get, set

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

