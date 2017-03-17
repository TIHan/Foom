namespace Foom.Network

open System
open System.Net
open System.Net.Sockets

type DesktopServer () =

    let sockets = ResizeArray<Socket> ()
    let tcp = new TcpListener (IPAddress.Any, 27015)

    interface IServer with

        member this.Start () =
            tcp.Start ()
            true

        member this.Heartbeat () =
            if tcp.Pending () then
                let socket = tcp.AcceptSocket ()
                printfn "Client connected: %A" socket.RemoteEndPoint
                sockets.Add socket

type DesktopClient () =

    let tcp = new TcpClient ()

    interface IClient with

        member this.Connect ip =
            async {
                let address = IPAddress.Parse ip
                do! tcp.ConnectAsync (address, 27015) |> Async.AwaitTask
                return true
            }

            