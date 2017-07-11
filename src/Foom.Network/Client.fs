namespace rec Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =
    inherit DataFlow (
        (fun packet -> udpClient.Send (packet.Raw, packet.Length) |> ignore),
        (fun packetPool send -> 
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
    )

    let mutable isConnected = false
    let connectedEvent = Event<IUdpEndPoint> ()

    member val Connected = connectedEvent.Publish

    member this.Connect (address, port) =
        if udpClient.Connect (address, port) then
            let packet = Packet ()
            packet.Type <- PacketType.ConnectionRequested

            udpClient.Send (packet.Raw, packet.Length) |> ignore
