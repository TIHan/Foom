namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let flow = DataFlow (fun packet -> udpClient.Send (packet.Raw, packet.Length) |> ignore)

    let mutable isConnected = false
    let connectedEvent = Event<IUdpEndPoint> ()

    member val Connected = connectedEvent.Publish

    member this.Connect (address, port) =
        if udpClient.Connect (address, port) then
            let packet = Packet ()
            packet.Type <- PacketType.ConnectionRequested

            udpClient.Send (packet.Raw, packet.Length) |> ignore

    member this.Subscribe<'T> f =
        flow.Subscribe<'T> f

    member this.Update time =
        flow.Update (time, fun packetPool send -> 
            if udpClient.IsDataAvailable then
                let packet = packetPool.Get ()
                let byteCount = udpClient.Receive (packet.Raw, 0, packet.Raw.Length)
                if byteCount > 0 then
                    packet.Length <- byteCount
                    send packet
                    true
                else
                    packetPool.Recycle packet
                    false
            else
                false
        )
