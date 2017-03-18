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
        client.Received.Add (fun (reader) ->
            str <- reader.ReadString ()
            //if str.Equals ("reliablestring") then
            //    failwith "wut ups"
        )

        server.Start () 
        |> ignore

        client.Connect ("127.0.0.1") 
        |> Async.RunSynchronously
        |> ignore

        server.Heartbeat ()

        server.DebugBroadcastReliableString ("reliablestring", 1001us)

        for i = 0 to 1000 do
            server.DebugBroadcastReliableString ("wrongwrong", uint16 i)

        client.Heartbeat ()

        let same = str = "reliablestring"
        Assert.True (same)
