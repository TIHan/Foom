namespace Foom.Network.Tests

open System
open System.Threading.Tasks

open NUnit.Framework

open Foom.Network

[<Struct>]
type TestStruct =

    val mutable X : int

    val mutable Y : int

    new (x, y) = { X = x; Y = y }

    static member Serialize (writer: IWriter) (data: TestStruct) =
        writer.Put data.X

    static member Deserialize (reader: IReader) =
        TestStruct (reader.GetInt (), reader.GetInt ())

[<TestFixture>]
type Test() = 

    [<Test>]
    member x.ConnectionWorks () =
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

    [<Test>]
    member x.DisconnectionWorks () =
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
            isConnected <- false
        )

        async {
            while not isDone do
                client.Update ()
                do! Async.Sleep 15
        }
        |> Async.RunSynchronously

        Assert.True (isConnected)

        isDone <- false

        client.Disconnect ()

        async {
            while not isDone do
                server.Update ()
                do! Async.Sleep 15
        }
        |> Async.Start

        async {
            while not isDone do
                client.Update ()
                do! Async.Sleep 15
        }
        |> Async.RunSynchronously

        Assert.False (isConnected)

    [<Test>]
    member x.SerializationWorks () =
        use server = new Server (64) :> IServer
        use client = new Client () :> IClient

        let mutable isDone = false
        let mutable isConnected = false

        server.RegisterType<TestStruct> (TestStruct.Serialize, TestStruct.Deserialize)
        client.RegisterType<TestStruct> (TestStruct.Serialize, TestStruct.Deserialize)
        client.Subscribe<TestStruct> (fun testStruct ->
            printfn "Test struct: %A" testStruct
            isDone <- true
        )

        Assert.True (server.Start (27015))



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
            isConnected <- false
        )

        async {
            while not isDone do
                client.Update ()
                do! Async.Sleep 15
        }
        |> Async.RunSynchronously

        Assert.True (isConnected)

        isDone <- false

        server.SendToAll<TestStruct> (TestStruct (1234, 5678))

        async {
            while not isDone do
                server.Update ()
                do! Async.Sleep 15
        }
        |> Async.Start

        async {
            while not isDone do
                client.Update ()
                do! Async.Sleep 15
        }
        |> Async.RunSynchronously