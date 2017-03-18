namespace Foom.Network

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Collections.Generic

type DesktopConnectedClient (endpoint: EndPoint) =

    interface IConnectedClient with

        member this.Address = endpoint.ToString ()
       
type DesktopServer () =

    let clientConnected = Event<IConnectedClient> ()
    let received = Event<IConnectedClient * BinaryReader> ()

    let udp = new UdpClient (27015)

    let lookup = Dictionary<IPAddress, EndPoint * DesktopConnectedClient> ()

    let buffer = Array.zeroCreate<byte> 65536

    let mutable reliableStringSequence = 0us

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

                    match Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<byte, ClientMessageType> (a) with
                    | ClientMessageType.ConnectionRequested -> 

                        if not <| lookup.ContainsKey ipendpoint.Address then
                            udp.Send ([| byte ServerMessageType.ConnectionEstablished |], 1, ipendpoint) |> ignore

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

        member val Received = received.Publish

        member this.BroadcastReliableString str =
            let bytes = System.Text.Encoding.UTF8.GetBytes (str)
            let seqBytes = BitConverter.GetBytes (reliableStringSequence)
            let bytes = Array.append seqBytes bytes 
            let bytes = Array.append [| byte ServerMessageType.ReliableOrder |] bytes
            lookup
            |> Seq.iter (fun pair ->
                let (endpoint, connectedClient) = pair.Value
                udp.Send (bytes, bytes.Length, endpoint :?> IPEndPoint) |> ignore
            )

            reliableStringSequence <- reliableStringSequence + 1us

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            (udp :> IDisposable).Dispose ()
            (this :> IServer).Stop ()
            lookup.Clear ()


type DesktopClient () =

    let udp = new UdpClient ()
    let ms = new MemoryStream (65536)
    let reader = new BinaryReader (ms)
    let received = Event<BinaryReader> ()

    let mutable mainEndpoint = null

    interface IClient with

        member this.Connect ip =
            async {
                let address = IPAddress.Parse ip
                mainEndpoint <- IPEndPoint (address, 27015)
                udp.Connect (mainEndpoint)
                udp.Send ([| byte ClientMessageType.ConnectionRequested |], 1) |> ignore
                return true
            }

        member this.Heartbeat () =
           // reader.BaseStream.Position <- 0L

            while udp.Available > 0 do
                reader.BaseStream.Position <- 0L
                let mutable endpoint = mainEndpoint :> EndPoint
                let bytes = udp.Client.ReceiveFrom (ms.GetBuffer (), &endpoint)
                ms.SetLength (int64 bytes)

                if bytes > 0 then
                    let a = reader.ReadByte ()

                    match Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<byte, ServerMessageType> (a) with
                    | ServerMessageType.ReliableOrder ->
                        let seqN = reader.ReadUInt16 ()

                        received.Trigger (reader)

                    | _ -> ()

        member val Received = received.Publish

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            reader.Close ()
            reader.Dispose ()
            ms.Close ()
            ms.Dispose ()
            (udp :> IDisposable).Dispose ()

            