namespace Foom.Network.Tests

open System

open NUnit.Framework

open Foom.Network

[<TestFixture>]
type Test() = 

    [<Test>]
    member x.StartClientAndServer () =
        use server = new DesktopServer () :> IServer
        use client = new DesktopClient () :> IClient

        let mutable str = ""
        server.ClientConnected.Add (fun s -> str <- s)

        server.Start () 
        |> ignore

        client.Connect ("127.0.0.1") 
        |> Async.RunSynchronously
        |> ignore

        server.Heartbeat ()
        Assert.True (str.Contains("127.0.0.1"))
