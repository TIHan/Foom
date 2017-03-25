namespace Foom.Network.Tests

open System
open System.Numerics
open System.Threading.Tasks
open System.Collections.Generic

open NUnit.Framework

open Foom.Network

[<Struct>]
type TestStruct =
    { 
        x: int
        y: int
    }

[<CLIMutable>]
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

[<CLIMutable>]
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

module TestMessage3 =

    let buffer = Array.zeroCreate<int> 65536

[<CLIMutable>]
type TestMessage3 =
    {
        mutable arr : int []
        mutable len : int
    }



    static member Serialize (msg: TestMessage3) (byteWriter: ByteWriter) =
        byteWriter.WriteInts (msg.arr, 0, msg.len)

    static member Deserialize (msg: TestMessage3) (byteReader: ByteReader) =
        msg.len <- byteReader.ReadInts (TestMessage3.buffer)
        msg.arr <- TestMessage3.buffer

[<TestFixture>]
type Test() = 

    do
        Network.RegisterType (TestMessage.Serialize, TestMessage.Deserialize)
        Network.RegisterType (TestMessage2.Serialize, TestMessage2.Deserialize)
        Network.RegisterType (TestMessage3.Serialize, TestMessage3.Deserialize)

    [<Test>]
    member this.UdpWorks () : unit =
        use udpClient = new UdpClient () :> IUdpClient
        use udpClientV4 = new UdpClient () :> IUdpClient
        use udpServer = new UdpServer (27015) :> IUdpServer

        Assert.False (udpClient.Connect ("break this", 27015))
        Assert.True (udpClient.Connect ("::1", 27015))
        Assert.True (udpClientV4.Connect ("127.0.0.1", 27015))

        for i = 0 to 100 do
            let i = 0
            Assert.True (udpClient.Send ([| 123uy + byte i |], 1) > 0)

            let buffer = [| 0uy |]
            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            while not udpServer.IsDataAvailable do ()
            Assert.True (udpServer.Receive (buffer, 0, 1, &endPoint) > 0)
            Assert.IsNotNull (endPoint)
            Assert.AreEqual (123uy + byte i, buffer.[0])

        let test amount =

            let maxBytes = Array.zeroCreate<byte> amount
            maxBytes.[amount - 1] <- 123uy

            Assert.True (udpClient.Send (maxBytes, maxBytes.Length) > 0)

            let buffer = Array.zeroCreate<byte> amount
            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            while not udpServer.IsDataAvailable do ()
            Assert.True (udpServer.Receive (buffer, 0, buffer.Length, &endPoint) > 0)
            Assert.IsNotNull (endPoint)
            Assert.AreEqual (123uy, buffer.[amount - 1])

        Assert.AreEqual (64512, udpClient.SendBufferSize)
        Assert.AreEqual (64512, udpClient.ReceiveBufferSize)
        Assert.AreEqual (64512, udpServer.SendBufferSize)
        Assert.AreEqual (64512, udpServer.ReceiveBufferSize)

        test 512
        test 1024
        test 8192
        udpClient.SendBufferSize <- 8193
        test 8193

        udpClient.SendBufferSize <- 10000
        test 10000

        for i = 1 to 64512 - 48 do
            udpClient.SendBufferSize <- i
            test i

    [<Test>]
    member this.ByteStream () : unit =

        let byteStream = ByteStream (1024)
        let byteWriter = ByteWriter (byteStream)
        let byteReader = ByteReader (byteStream)

        let mutable testStruct = { x = 1234; y = 5678 }
        let mutable testStruct2 = { x = 0; y = 0 }

        let ops =
            [
                (byteWriter.WriteInt (Int32.MaxValue), fun () -> Assert.AreEqual (Int32.MaxValue, byteReader.ReadInt ()))
                (byteWriter.WriteInt (Int32.MinValue), fun () -> Assert.AreEqual (Int32.MinValue, byteReader.ReadInt ()))

                (byteWriter.WriteUInt32 (UInt32.MaxValue), fun () -> Assert.AreEqual (UInt32.MaxValue, byteReader.ReadUInt32 ()))
                (byteWriter.WriteUInt32 (UInt32.MinValue), fun () -> Assert.AreEqual (UInt32.MinValue, byteReader.ReadUInt32 ()))

                (byteWriter.WriteSingle (5.388572987598298734987f), fun () -> Assert.AreEqual (5.388572987598298734987f, byteReader.ReadSingle ()))

                (byteWriter.Write (testStruct), fun () -> Assert.AreEqual (testStruct, byteReader.Read<TestStruct> ()))
                (byteWriter.Write (&testStruct), fun () -> 
                    Assert.AreNotEqual (testStruct, testStruct2)
                    let t = byteReader.Read<TestStruct> (&testStruct2)
                    Assert.AreEqual (testStruct, testStruct2)
                )

                (byteWriter.WriteSingle (7.38857298759829874987f), fun () -> Assert.AreEqual (7.38857298759829874987f, byteReader.ReadSingle ()))
            ]

        byteStream.Position <- 0

        ops
        |> List.iter (fun (_, test) ->
            test ()
        )

    [<Test>]
    member this.ClientAndServerWorks () : unit =
        use udpClient = new UdpClient () :> IUdpClient
        use udpClientV6 = new UdpClient () :> IUdpClient
        use udpServer = new UdpServer (27015) :> IUdpServer

        let client = Client (udpClient)
        let clientV6 = Client (udpClientV6)
        let server = Server (udpServer)

        let mutable isConnected = false
        let mutable isIpv6Connected = false
        let mutable clientDidConnect = false
        let mutable messageReceived = false
        let mutable endOfArray = 0

        client.Connected.Add (fun endPoint -> 
            isConnected <- true
            printfn "[Client] connected to %s." endPoint.IPAddress
        )
        clientV6.Connected.Add (fun endPoint -> 
            isIpv6Connected <- true
            printfn "[Client] connected to %s." endPoint.IPAddress
        )
        server.ClientConnected.Add (fun endPoint -> 
            clientDidConnect <- true
            printfn "[Server] Client connected from %s." endPoint.IPAddress
        )

        client.Connect ("127.0.0.1", 27015)
        clientV6.Connect ("::1", 27015)
        client.Update ()
        clientV6.Update ()
        server.Update ()
        client.Update ()
        clientV6.Update ()

        Assert.True (isConnected)
        Assert.True (isIpv6Connected)
        Assert.True (clientDidConnect)

        client.Subscribe<TestMessage2> (fun msg -> 
            messageReceived <- true
           // printfn "%A" msg
        )
        clientV6.Subscribe<TestMessage2> (fun msg -> 
            messageReceived <- true
           // printfn "%A" msg
        )

        client.Subscribe<TestMessage> (fun msg -> 
            messageReceived <- true
           // printfn "%A" msg
        )
        clientV6.Subscribe<TestMessage> (fun msg -> 
            messageReceived <- true
           // printfn "%A" msg
        )


        client.Subscribe<TestMessage3> (fun msg ->
            endOfArray <- msg.arr.[msg.len - 1]
        )

        server.Publish ({ a = 9898; b = 3456 })

        let data = { arr = Array.zeroCreate 200; len = 200 }

        data.arr.[data.len - 1] <- 808

        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

        //for i = 0 to 50 do
        for i = 0 to 10 do
            server.Publish ({ a = 9898; b = 3456 })
            server.Publish ({ c = 1337; d = 666 })

        server.Update ()

        stopwatch.Stop ()

        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

        for i = 0 to 40 do
            server.Publish data

        server.Update ()

        stopwatch.Stop ()

        printfn "[Server] %f kB sent." (single server.BytesSentSinceLastUpdate / 1024.f)
        printfn "[Server] time taken: %A." stopwatch.Elapsed.TotalMilliseconds

        client.Update ()
        clientV6.Update ()

        Assert.True (messageReceived)
        Assert.AreEqual (808, endOfArray)


    [<Test>]
    member x.TestReliableOrderedChannel () =
        let packetPool = PacketPool 64
        let channel = ReliableOrderedChannel packetPool

        let byteStream = ByteStream 1024
        let byteWriter = ByteWriter byteStream

        byteWriter.WriteInt (1357)

        let queue = Queue ()

        for i = 0 to 100000 do
            for i = 0 to 27 do
                channel.ProcessData (byteStream.Raw, 0, byteStream.Length, fun packet ->
                  queue.Enqueue packet
                )

            while queue.Count > 0 do
                let packet = queue.Dequeue ()
                channel.Ack (packet.SequenceId)
                packetPool.Recycle packet

        Assert.False (channel.HasPendingAcks)


        // Test Resending based on time.

        for i = 0 to 27 do
            channel.ProcessData (byteStream.Raw, 0, byteStream.Length, fun packet ->
                packetPool.Recycle packet
            )

        Assert.True (channel.HasPendingAcks)

        channel.Update (fun packet ->
            queue.Enqueue packet
        )

        while queue.Count > 0 do
            let packet = queue.Dequeue ()
            channel.Ack (packet.SequenceId)
            packetPool.Recycle packet

        Assert.True (channel.HasPendingAcks)

        System.Threading.Thread.Sleep (11000)

        channel.Update (fun packet ->
            queue.Enqueue packet
        )

        while queue.Count > 0 do
            let packet = queue.Dequeue ()
            channel.Ack (packet.SequenceId)
            packetPool.Recycle packet

        Assert.False (channel.HasPendingAcks)





 