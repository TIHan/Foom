namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let peer = Peer (Udp.Client udpClient)

    let mutable isConnected = false
    let connectedEvent = Event<IUdpEndPoint> ()

    member val Connected = connectedEvent.Publish

    member this.Connect (address, port) =
        if udpClient.Connect (address, port) then
            let packet = Packet ()
            packet.Type <- PacketType.ConnectionRequested

            udpClient.Send (packet.Raw, packet.Length) |> ignore

    member this.Subscribe<'T> f =
        peer.Subscribe<'T> f

    member this.Update time =
        peer.Update time
