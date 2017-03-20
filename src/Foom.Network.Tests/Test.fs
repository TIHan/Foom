namespace Foom.Network.Tests

open System
open System.Threading.Tasks

open NUnit.Framework

open Foom.Network

[<TestFixture>]
type Test() = 

    [<Test>]
    member x.StartClientAndServer () =
        use server = new Server (64) :> IServer
        use client = new Client () :> IClient

        Assert.True (server.Start (27015))

        let mutable isDone = false
        let mutable isConnected = false

        async {
            while not isDone do
                server.Update ()
                do! Async.Sleep 15
        }
        |> Async.Start

        client.Connect ("localhost", 27015)

        client.Connected.Add (fun () ->
            isDone <- true
            isConnected <- true
        )

        client.Disconnected.Add (fun () ->
            isDone <- true
        )

        async {
            while not isDone do
                client.Update ()
                do! Async.Sleep 15
        }
        |> Async.RunSynchronously

        Assert.True (isConnected)
