open System

open Foom.Network

[<Struct>]
type TestStruct =
    { 
        x: int
        y: int
    }

type TestMessage =
    {
        mutable a : int
        mutable b : int
    }

    static member Serialize (msg: TestMessage) (byteWriter: ByteWriter) =
        byteWriter.WriteInt (msg.a)
        byteWriter.WriteInt (msg.b)

    static member Deserialize (msg: TestMessage) (byteReader: ByteReader) =
        msg.a <- byteReader.ReadInt ()
        msg.b <- byteReader.ReadInt ()

    static member Ctor _ =
        {
            a = 0
            b = 0
        }

type TestMessage2 =
    {
        mutable c : int
        mutable d : int
    }

    static member Serialize (msg: TestMessage2) (byteWriter: ByteWriter) =
        byteWriter.WriteInt (msg.c)
        byteWriter.WriteInt (msg.d)

    static member Deserialize (msg: TestMessage2) (byteReader: ByteReader) =
        msg.c <- byteReader.ReadInt ()
        msg.d <- byteReader.ReadInt ()

    static member Ctor _ =
        {
            c = 0
            d = 0
        }

module TestMessage3 =

    let buffer = Array.zeroCreate<int> 65536

type TestMessage3 =
    {
        mutable arr : int []
        mutable len : int
    }



    static member Serialize (msg: TestMessage3) (byteWriter: ByteWriter) =
        byteWriter.WriteInts (msg.arr, 0, msg.len)

    static member Deserialize (msg: TestMessage3) (byteReader: ByteReader) =
        msg.len <- byteReader.ReadInts (msg.arr)

    static member Ctor _ =
        {
            arr = TestMessage3.buffer
            len = 0
        }

Network.RegisterType (TestMessage.Serialize, TestMessage.Deserialize, TestMessage.Ctor)
Network.RegisterType (TestMessage2.Serialize, TestMessage2.Deserialize, TestMessage2.Ctor)
Network.RegisterType (TestMessage3.Serialize, TestMessage3.Deserialize, TestMessage3.Ctor)

let perf title f =
    let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
    f ()
    stopwatch.Stop ()
    printfn "%s: %A" title stopwatch.Elapsed.TotalMilliseconds

[<EntryPoint>]
let main argv = 
    use udpClient = new UdpClient () :> IUdpClient
    use udpServer = new UdpServer (29015) :> IUdpServer

    let client = Client (udpClient, new DeflateCompression ())
    let mutable value = 0
    client.Subscribe<TestMessage> (fun msg ->
        value <- msg.b
    )

    let server = Server (udpServer, new DeflateCompression ())

    client.Connect ("127.0.0.1", 29015)

    client.Update TimeSpan.Zero
    Threading.Thread.Sleep 100
    server.Update TimeSpan.Zero
    Threading.Thread.Sleep 100
    client.Update TimeSpan.Zero

    let testSize = 4096 * 4

    let values = Array.zeroCreate testSize

    client.Subscribe<TestMessage3> (fun msg ->
        for i = 0 to msg.len - 1 do
            values.[i] <- msg.arr.[i]
    )

    let data = { arr = Array.init testSize (fun i -> i + 1); len = testSize }

    
    let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
    for i = 1 to 50 do
        perf "Server Send" (fun () ->
            server.PublishReliableOrdered data
            server.Update stopwatch.Elapsed
        )
        Threading.Thread.Sleep 10

        perf "Client Receive" (fun () ->
            client.Update stopwatch.Elapsed
        )

        printfn "%A = %A" client.PacketPoolMaxCount client.PacketPoolCount

    Console.ReadLine () |> ignore
    0

