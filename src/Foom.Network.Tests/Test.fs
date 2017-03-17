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
        server.ClientConnected.Add (fun s -> str <- s.Address)

        server.Start () 
        |> ignore

        client.Connect ("127.0.0.1") 
        |> Async.RunSynchronously
        |> ignore

        server.Heartbeat ()
        Assert.True (str.Contains("127.0.0.1"))

    [<Test>]
    member x.SendAndReceiveReliableString () =
        use server = new DesktopServer () :> IServer
        use client = new DesktopClient () :> IClient

        let mutable str = ""
        server.ClientPacketReceived.Add (fun (_, packet) ->
            str <- packet.ReadReliableString ()
            str <-
                str.Split ([|';'|])
                |> Array.last
        )

        server.Start () 
        |> ignore

        client.Connect ("127.0.0.1") 
        |> Async.RunSynchronously
        |> ignore

        for i = 1 to 10000 do
            client.SendReliableString ("wrong;")

        client.SendReliableString ("reliablestring")

        server.Heartbeat ()

        let same = str = "reliablestring"
        Assert.True (same)
