namespace Foom.Network

open System
open System.Net
open System.Net.Sockets

type DesktopServer () =

    let clientConnected = Event<string> ()
    let sockets = ResizeArray<Socket> ()
    let tcp = TcpListener (IPAddress.Any, 27015)

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
                sockets.Add socket
                clientConnected.Trigger (socket.RemoteEndPoint.ToString ())

        member val ClientConnected = clientConnected.Publish

    interface IDisposable with

        member this.Dispose () =
            (this :> IServer).Stop ()
            sockets
            |> Seq.iter (fun s ->
                s.Close ()
                s.Dispose ()
            )
            sockets.Clear ()


type DesktopClient () =

    let tcp = new TcpClient ()

    interface IClient with

        member this.Connect ip =
            async {
                let address = IPAddress.Parse ip
                do! tcp.ConnectAsync (address, 27015) |> Async.AwaitTask
                return true
            }

    interface IDisposable with

        member this.Dispose () =
            tcp.Close ()
            (tcp :> IDisposable).Dispose ()

            