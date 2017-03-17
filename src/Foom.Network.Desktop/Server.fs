namespace Foom.Network

open System
open System.Net
open System.Net.Sockets
open System.Collections.Generic

type DesktopPacket (buffer, index, count) =

    member val Count = count

    member val Buffer = buffer

    interface IPacket with

        member this.ReadReliableString () =
            System.Text.Encoding.UTF8.GetString(buffer, index, count)

type DesktopConnectedClient (endpoint: EndPoint) =

    interface IConnectedClient with

        member this.Address = endpoint.ToString ()

type ClientMessage =
    | ConnectionRequested = 0uy
    //| ReliableString = 1uy

type ServerMessage =
    | ConnectionEstablished = 0uy
    | ReliableString = 1uy
       
type DesktopServer () =

    let clientConnected = Event<IConnectedClient> ()
    let clientPacketReceived = Event<IConnectedClient * IPacket> ()

    let udp = new UdpClient (27015)

    let lookup = Dictionary<IPAddress, EndPoint * DesktopConnectedClient> ()

    let buffer = Array.zeroCreate<byte> 65536

    interface IServer with

        member this.Start () =
            printfn "Starting server..."

           // tcp.Start ()

        member this.Stop () =
            printfn "Stopping server..."

            //tcp.Stop ()

        member this.Heartbeat () =
            while udp.Available > 0 do
                let ipendpoint = IPEndPoint (IPAddress.Any, 0)
                let mutable endpoint = ipendpoint :> EndPoint
                let bytes = udp.Client.ReceiveFrom (buffer, &endpoint)
                let ipendpoint : IPEndPoint = downcast endpoint

                if bytes > 0 then
                    let a = buffer.[0]

                    match Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<byte, ClientMessage> (a) with
                    | ClientMessage.ConnectionRequested -> 

                        if not <| lookup.ContainsKey ipendpoint.Address then
                            udp.Send ([| byte ServerMessage.ConnectionEstablished |], 1, ipendpoint) |> ignore

                            let connectedClient = DesktopConnectedClient (endpoint)
                            let tup = (endpoint, connectedClient)
                            lookup.[ipendpoint.Address] <- tup

                            clientConnected.Trigger (connectedClient)

                    //| ClientMessage.ReliableString ->

                    //    match lookup.TryGetValue ipendpoint.Address with
                    //    | true, (endpoint, connectedClient) ->
                    //        let packet = DesktopPacket (buffer, 1, bytes - 1) :> IPacket
                    //        clientPacketReceived.Trigger (connectedClient :> IConnectedClient, packet)
                    //    | _ -> ()

                    | _ -> ()

        member val ClientConnected = clientConnected.Publish

        member val ClientPacketReceived = clientPacketReceived.Publish

        member this.BroadcastReliableString str =
            let bytes = System.Text.Encoding.UTF8.GetBytes (str)
            let bytes = Array.append [| byte ServerMessage.ReliableString |] bytes 
            lookup
            |> Seq.iter (fun pair ->
                let (endpoint, connectedClient) = pair.Value
                udp.Send (bytes, bytes.Length, endpoint :?> IPEndPoint) |> ignore
            )

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            (udp :> IDisposable).Dispose ()
            (this :> IServer).Stop ()
            lookup.Clear ()


type DesktopClient () =

    let udp = new UdpClient ()

    let buffer = Array.zeroCreate<byte> 65536

    let serverPacketReceived = Event<IPacket> ()

    let mutable mainEndpoint = null

    interface IClient with

        member this.Connect ip =
            async {
                let address = IPAddress.Parse ip
                mainEndpoint <- IPEndPoint (address, 27015)
                udp.Connect (mainEndpoint)
                udp.Send ([| byte ClientMessage.ConnectionRequested |], 1) |> ignore
                return true
            }

        member this.Heartbeat () =
            while udp.Available > 0 do
                let mutable endpoint = mainEndpoint :> EndPoint
                let bytes = udp.Client.ReceiveFrom (buffer, &endpoint)

                if bytes > 0 then
                    let a = buffer.[0]

                    match Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<byte, ServerMessage> (a) with
                    | ServerMessage.ReliableString ->

                        let packet = DesktopPacket (buffer, 1, bytes - 1) :> IPacket
                        serverPacketReceived.Trigger (packet)

                    | _ -> ()

        member val ServerPacketReceived = serverPacketReceived.Publish

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            (udp :> IDisposable).Dispose ()

            