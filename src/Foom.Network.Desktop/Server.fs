namespace Foom.Network

open System
open System.Net
open System.Net.Sockets
open System.Collections.Generic

type DesktopPacket (count: int, buffer: byte []) =

    member val Count = count

    member val Buffer = buffer

    interface IPacket with

        member this.ReadReliableString () =
            System.Text.Encoding.UTF8.GetString(buffer, 0, count)

type DesktopConnectedClient (id: int, socket: Socket) =

    interface IConnectedClient with

        member this.Id = id

        member this.Address = socket.RemoteEndPoint.ToString ()
       
type DesktopServer () =

    let clientConnected = Event<IConnectedClient> ()
    let clientPacketReceived = Event<IConnectedClient * IPacket> ()

    let tcp = TcpListener (IPAddress.Any, 27015)

    let lookup = Dictionary<int, Socket * IConnectedClient> ()

    let buffer = Array.zeroCreate<byte> 65536

    let mutable nextClientId = 0

    interface IServer with

        member this.Start () =
            printfn "Starting server..."

            tcp.Start ()

        member this.Stop () =
            printfn "Stopping server..."

            tcp.Stop ()

        member this.Heartbeat () =
            if tcp.Pending () then
                let socket = tcp.AcceptSocket ()
                let connectedClient = DesktopConnectedClient (nextClientId, socket) :> IConnectedClient

                lookup.[nextClientId] <- (socket, connectedClient)
                clientConnected.Trigger (connectedClient)
                nextClientId <- nextClientId + 1

            lookup
            |> Seq.iter (fun pair ->
                let (s, connectedClient) = pair.Value
                if s.Connected && s.Poll (0, SelectMode.SelectRead) then
                    let mutable endpoint = s.RemoteEndPoint
                    let bytes = s.ReceiveFrom (buffer, &endpoint)
                    if bytes > 0 then
                        let packet = DesktopPacket (bytes, buffer) :> IPacket
                        clientPacketReceived.Trigger (connectedClient, packet)
            )

        member val ClientConnected = clientConnected.Publish

        member val ClientPacketReceived = clientPacketReceived.Publish

    interface IDisposable with

        member this.Dispose () =
            (this :> IServer).Stop ()
            lookup
            |> Seq.iter (fun pair ->
                let (s, _) = pair.Value
                s.Close ()
                s.Dispose ()
            )
            lookup.Clear ()


type DesktopClient () =

    let tcp = new TcpClient ()

    interface IClient with

        member this.Connect ip =
            async {
                let address = IPAddress.Parse ip
                do! tcp.ConnectAsync (address, 27015) |> Async.AwaitTask
                return true
            }

        member this.SendReliableString msg =
            let stream = tcp.GetStream ()

            let bytes = System.Text.Encoding.UTF8.GetBytes (msg)
            stream.Write (bytes, 0, bytes.Length)

    interface IDisposable with

        member this.Dispose () =
            tcp.Close ()
            (tcp :> IDisposable).Dispose ()

            