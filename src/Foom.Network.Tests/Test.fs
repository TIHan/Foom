namespace Foom.Network.Tests

open System
open System.Threading.Tasks

open NUnit.Framework

open Foom.Network

[<Struct>]
type TestStruct =
    {
        X: int
        Y: int
    }

[<TestFixture>]
type Test() = 

    [<Test>]
    member this.UdpWorks () : unit =
        use udpClient = new UdpClient () :> IUdpClient
        use udpServer = new UdpServer (27015) :> IUdpServer

        Assert.False (udpClient.Connect ("break this", 27015))
        Assert.True (udpClient.Connect ("localhost", 27015))

        for i = 0 to 100 do
            let i = 0
            Assert.Greater (udpClient.Send ([| 123uy + byte i |], 1), 0)

            let buffer = [| 0uy |]
            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            while not udpServer.IsDataAvailable do ()
            Assert.Greater (udpServer.Receive (buffer, 0, 1, &endPoint), 0)
            Assert.IsNotNull (endPoint)
            Assert.AreEqual (123uy + byte i, buffer.[0])

        let test amount =

            let maxBytes = Array.zeroCreate<byte> amount
            maxBytes.[amount - 1] <- 123uy

            Assert.Greater (udpClient.Send (maxBytes, maxBytes.Length), 0)

            let buffer = Array.zeroCreate<byte> amount
            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            while not udpServer.IsDataAvailable do ()
            Assert.Greater (udpServer.Receive (buffer, 0, buffer.Length, &endPoint), 0)
            Assert.IsNotNull (endPoint)
            Assert.AreEqual (123uy, buffer.[amount - 1])

        Assert.AreEqual (8192, udpClient.SendBufferSize)
        Assert.AreEqual (65535, udpClient.ReceiveBufferSize)
        Assert.AreEqual (8192, udpServer.SendBufferSize)
        Assert.AreEqual (65535, udpServer.ReceiveBufferSize)

        test 512
        test 1024
        test 8192
        udpClient.SendBufferSize <- 8193
        test 8193

        udpClient.SendBufferSize <- 10000
        test 10000

        udpClient.SendBufferSize <- 65000
        test 65000

        udpClient.SendBufferSize <- 65487
        test 65487

        for i = 1 to 65535 - 48 do
            udpClient.SendBufferSize <- i
            test i

    [<Test>]
    member this.ByteStream () : unit =

        let byteStream = ByteStream (1024)
        let byteWriter = ByteWriter (byteStream)
        let byteReader = ByteReader (byteStream)

        let mutable testStruct = { X = 1234; Y = 5678 }
        let mutable testStruct2 = { X = 0; Y = 0 }

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
        use udpServer = new UdpServer (27015) :> IUdpServer

        let client = Client (udpClient)
        let server = Server (udpServer)

        let mutable isConnected = false

        client.Connected.Add (fun () -> isConnected <- true)

        client.Connect ("localhost", 27015)
        client.Update ()
        server.Update ()
        client.Update ()

        Assert.True (isConnected)
