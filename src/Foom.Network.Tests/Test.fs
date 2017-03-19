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
        )

        server.Start () 
        |> ignore

        client.Connect ("127.0.0.1") 
        |> Async.RunSynchronously
        |> ignore

        let msg = server.CreateMessage ()
        msg.Writer.Write ("wrong")

        server.SendMessage (msg)

        let msg = server.CreateMessage ()
        msg.Writer.Write ("reliablestring")

        server.SendMessage (msg)

        server.Heartbeat ()
        client.Heartbeat ()

        //System.Threading.Thread.Sleep(100)

        //server.Heartbeat ()

        let same = str = "reliablestring"
        Assert.True (same)
