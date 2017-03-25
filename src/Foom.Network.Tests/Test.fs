namespace Foom.Network.Tests

open System
open System.Numerics
open System.Threading.Tasks

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

[<TestFixture>]
type Test() = 

    do
        Network.RegisterType (TestMessage.Serialize, TestMessage.Deserialize)
        Network.RegisterType (TestMessage2.Serialize, TestMessage2.Deserialize)

    //[<Test>]
    //member this.PacketMergingWorks () : unit =
    //    let p1 = Packet ()
    //    let p2 = Packet ()



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

        for i = 0 to 128 do
            server.Publish ({ a = 9898; b = 3456 })
            server.Publish ({ c = 1337; d = 666 })

        server.Update ()
        client.Update ()
       // clientV6.Update ()

        Assert.True (messageReceived)
