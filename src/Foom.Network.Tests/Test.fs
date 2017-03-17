namespace Foom.Network.Tests

open System

open NUnit.Framework

open Foom.Network

[<TestFixture>]
type Test() = 

    [<Test>]
    member x.TestCase() =
        let server = DesktopServer () :> IServer
        let client = DesktopClient () :> IClient

        server.Start () 
        |> ignore

        client.Connect ("127.0.0.1") 
        |> Async.RunSynchronously
        |> ignore

        server.Heartbeat ()

