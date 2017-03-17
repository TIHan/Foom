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

type DesktopConnectedClient (id: int, endpoint: EndPoint) =

    interface IConnectedClient with

        member this.Id = id

        member this.Address = endpoint.ToString ()

type ClientMessage =
    | ConnectionRequested = 0uy
    | ReliableString = 1uy

type ServerMessage =
    | ConnectionEstablished = 0uy
       
type DesktopServer () =

    let clientConnected = Event<IConnectedClient> ()
    let clientPacketReceived = Event<IConnectedClient * IPacket> ()

    let udp = new UdpClient (27015)

    let lookup = Dictionary<IPAddress, EndPoint * DesktopConnectedClient> ()

    let buffer = Array.zeroCreate<byte> 65536

    let mutable nextClientId = 0

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

                            let connectedClient = DesktopConnectedClient (nextClientId, endpoint)
                            let tup = (endpoint, connectedClient)
                            lookup.[ipendpoint.Address] <- tup

                            clientConnected.Trigger (connectedClient)

                    | ClientMessage.ReliableString ->

                        match lookup.TryGetValue ipendpoint.Address with
                        | true, (endpoint, connectedClient) ->
                            let packet = DesktopPacket (buffer, 1, bytes - 1) :> IPacket
                            clientPacketReceived.Trigger (connectedClient :> IConnectedClient, packet)
                        | _ -> ()

                    | _ -> ()

        member val ClientConnected = clientConnected.Publish

        member val ClientPacketReceived = clientPacketReceived.Publish

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            (udp :> IDisposable).Dispose ()
            (this :> IServer).Stop ()
            lookup.Clear ()


type DesktopClient () =

    let udp = new UdpClient ()

    interface IClient with

        member this.Connect ip =
            async {
                let address = IPAddress.Parse ip
                udp.Connect (IPEndPoint (address, 27015))
                udp.Send ([| byte ClientMessage.ConnectionRequested |], 1) |> ignore
                return true
            }

        member this.SendReliableString msg =

            let bytes = System.Text.Encoding.UTF8.GetBytes (msg)
            udp.Send (Array.append [| byte ClientMessage.ReliableString |]bytes, 1 + bytes.Length) |> ignore

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            (udp :> IDisposable).Dispose ()

            