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

[<TestFixture>]
type Test() = 

    do
        Network.RegisterType (TestMessage.Serialize, TestMessage.Deserialize, TestMessage.Ctor)
        Network.RegisterType (TestMessage2.Serialize, TestMessage2.Deserialize, TestMessage2.Ctor)
        Network.RegisterType (TestMessage3.Serialize, TestMessage3.Deserialize, TestMessage3.Ctor)

    [<Test>]
    member this.UdpWorks () : unit =
        use udpClient = new UdpClient () :> IUdpClient
        use udpClientV4 = new UdpClient () :> IUdpClient
        use udpServer = new UdpServer (29015) :> IUdpServer

        Assert.False (udpClient.Connect ("break this", 29015))
        Assert.True (udpClient.Connect ("::1", 29015))
        Assert.True (udpClientV4.Connect ("127.0.0.1", 29015))

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
        use udpServer = new UdpServer (29015) :> IUdpServer

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

        client.Connect ("127.0.0.1", 29015)
        clientV6.Connect ("::1", 29015)
        client.Update ()
        clientV6.Update ()
        server.Update ()
        client.Update ()
        clientV6.Update ()

       // Assert.True (isConnected)
       // Assert.True (isIpv6Connected)
       // Assert.True (clientDidConnect)

        client.Subscribe<TestMessage2> (fun msg -> 
            messageReceived <- true
            printfn "%A" msg
        )
        clientV6.Subscribe<TestMessage2> (fun msg -> 
            messageReceived <- true
            printfn "%A" msg
        )

        client.Subscribe<TestMessage> (fun msg -> 
            messageReceived <- true
            printfn "%A" msg
        )
        clientV6.Subscribe<TestMessage> (fun msg -> 
            messageReceived <- true
            printfn "%A" msg
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

        printfn "[Server] %f kB sent." (single server.BytesSentSinceLastUpdate / 1024.f)
        printfn "[Server] time taken: %A." stopwatch.Elapsed.TotalMilliseconds

        //for i = 1 to 500 do
        //    use udpClient = new UdpClient () :> IUdpClient
        //    let client = Client (udpClient)
        //    client.Connect ("127.0.0.1", 27015)
        //    client.Update ()

        server.Update ()

        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

        for i = 0 to 1 do
            server.Publish data

        server.Update ()

        stopwatch.Stop ()

        printfn "[Server] %f kB sent." (single server.BytesSentSinceLastUpdate / 1024.f)
        printfn "[Server] time taken: %A." stopwatch.Elapsed.TotalMilliseconds

        for i = 0 to 1000 do


            for i = 0 to 40 do
                server.Publish data

            server.Update ()

            let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

            client.Update ()
            clientV6.Update ()

            stopwatch.Stop ()

            printfn "[Server] %f kB sent." (single server.BytesSentSinceLastUpdate / 1024.f)
            printfn "[Server] time taken: %A." stopwatch.Elapsed.TotalMilliseconds


        Assert.True (messageReceived)
        Assert.AreEqual (808, endOfArray)

    [<Test>]
    member x.TestReceiver () =
        let byteStream = ByteStream (1024)
        let byteWriter = ByteWriter (byteStream)
        let byteReader = ByteReader (byteStream)

        byteWriter.Write { x = 1234; y = 5678 }

        let packetPool = PacketPool 64
        let ackManager = AckManager ()

        let mutable ackId = -1

        let reliableOrderedReceiver = ReliableOrderedAckReceiver packetPool ackManager (fun i -> ackId <- int i)

        Assert.AreEqual (-1, ackId)

        let inputs = ResizeArray ()
        let test seqN =

            let packet = packetPool.Get ()

            packet.WriteRawBytes (byteStream.Raw, 0, byteStream.Length)
            packet.Type <- PacketType.ReliableOrdered
            packet.SequenceId <- seqN

            inputs.Add packet

            reliableOrderedReceiver inputs packetPool.Recycle

            inputs.Clear ()

        test 0us
        Assert.AreEqual (0us, ackId)
        test 1us
        Assert.AreEqual (1us, ackId)
        test 2us
        Assert.AreEqual (2us, ackId)

        test 10us
        Assert.AreEqual (2us, ackId)
        test 9us
        test 8us
        test 7us
        Assert.AreEqual (2us, ackId)
        test 6us
        test 5us
        test 4us
        Assert.AreEqual (2us, ackId)
        test 3us
        test 3us
        test 2us
        Assert.AreEqual (10us, ackId)

    [<Test>]
    member this.NewPipeline () =

        let filter1 = Pipeline.map (fun (x : int) -> double x)
        let filter2 = Pipeline.map (fun (x : double) -> string (x + 1.0))
        let filter3 = Pipeline.map (fun (x : string) -> System.Int32.Parse x)

        let x = 1
        let mutable y = 0
        let pipeline =
            Pipeline.create ()
            |> filter1
            |> filter2
            |> filter3
            |> Pipeline.sink (fun x -> 
                y <- x
            )
            |> Pipeline.build

        pipeline.Send x
        pipeline.Process ()

        Assert.AreEqual (x + 1, y)

    [<Test>]
    member this.TestPacket () =

        let packet = Packet ()

        packet.SequenceId <- 567us
        packet.FragmentId <- 77us
        packet.Type <- PacketType.ReliableAck

        Assert.AreEqual (packet.SequenceId, 567us)
        Assert.AreEqual (packet.FragmentId, 77us)
        Assert.AreEqual (packet.Type, PacketType.ReliableAck)


    [<Test>]
    member this.DataPipelineTest () =

        let data1 = { bytes = Array.zeroCreate 128; startIndex = 0; size = 128 }
        let data2 = { bytes = Array.zeroCreate 128; startIndex = 0; size = 128 }

        let packetPool = PacketPool 64

        let packets = ResizeArray ()
        let mergeFilter = createMergeFilter packetPool
        let filter1 = Pipeline.filter mergeFilter

        let packets = ResizeArray ()

        let pipeline =
            Pipeline.create ()
            |> filter1
            |> Pipeline.sink packets.Add
            |> Pipeline.build

        pipeline.Send data1
        pipeline.Send data2

        pipeline.Process ()

        Assert.AreEqual (packets.Count, 1)

        packets
        |> Seq.iter packetPool.Recycle
        packets.Clear ()

        for i = 1 to 100 do
            pipeline.Send data1
            pipeline.Send data2

        pipeline.Process ()

        Assert.AreEqual (packets.Count, 25)

        packets
        |> Seq.iter packetPool.Recycle
        packets.Clear ()

    [<Test>]
    member this.DataPipelineTestFragmented () =
        let packetPool = PacketPool 64

        let packets = ResizeArray ()
        let mergeFilter = createMergeFilter packetPool
        let filter1 = Pipeline.filter mergeFilter

        let packets = ResizeArray ()

        let pipeline =
            Pipeline.create ()
            |> filter1
            |> Pipeline.sink packets.Add
            |> Pipeline.build

        let data3 = { bytes = Array.zeroCreate 12800; startIndex = 0; size = 12800 }

        data3.bytes.[12800 - 1] <- 129uy

        pipeline.Send data3

        pipeline.Process ()

        let lastPacket = packets.[packets.Count - 1]

        Assert.AreEqual (lastPacket.Raw.[lastPacket.Length - 1], 129uy)

    [<Test>]
    member this.ReliableOrderedPipelines () =
        let packetPool = PacketPool 64

        let sender = Sender.createReliableOrdered packetPool

        let packetPool = PacketPool 64

        let receiver = Receiver.createReliableOrdered packetPool (fun ack -> ())

        let packets = ResizeArray ()

        sender.Output.Add receiver.Send

        receiver.Output.Add packets.Add

        let data1 = { bytes = Array.zeroCreate 128; startIndex = 0; size = 128 }
        let data2 = { bytes = Array.zeroCreate 12800; startIndex = 0; size = 12800 }

        data2.bytes.[12800 - 1] <- 129uy

        for i = 1 to 100 do
            sender.Send data1

        sender.Send data2

        sender.Process ()
        receiver.Process ()

        let lastPacket = packets.[packets.Count - 1]

        Assert.AreEqual (lastPacket.Raw.[lastPacket.Length - 1], 129uy)
        


