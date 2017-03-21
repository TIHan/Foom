namespace Foom.Network.Tests

open System
open System.Threading.Tasks

open NUnit.Framework

open Foom.Network

[<TestFixture>]
type Test() = 

    [<Test>]
    member this.TestUdp () : unit =
        use udpClient = new UdpClient () :> IUdpClient
        use udpServer = new UdpServer (27015) :> IUdpServer

        Assert.False (udpClient.Connect ("break this", 27015))
        Assert.True (udpClient.Connect ("localhost", 27015))

        for i = 0 to 100 do

            Assert.Greater (udpClient.Send ([| 123uy + byte i |], 1), 0)

            let buffer = [| 0uy |]
            let mutable endPoint = Unchecked.defaultof<IUdpEndPoint>
            Assert.Greater (udpServer.Receive (buffer, 1, &endPoint), 0)
            Assert.IsNotNull (endPoint)
            Assert.AreEqual (123uy + byte i, buffer.[0])

            Assert.Greater (udpServer.Send ([| 33uy + byte i |], 1, endPoint), 0)
            Assert.Greater (udpClient.Receive (buffer, 1), 0)
            Assert.AreEqual (33uy + byte i, buffer.[0])


    [<Test>]
    member this.ReadWriteStreamWorks () : unit =

        let byteStream = ByteStream (1024)
        let byteWriter = ByteWriter (byteStream)
        let byteReader = ByteReader (byteStream)

        let ops =
            [
                (byteWriter.WriteInt (Int32.MaxValue), fun () -> Assert.AreEqual (Int32.MaxValue, byteReader.ReadInt ()))
                (byteWriter.WriteInt (Int32.MinValue), fun () -> Assert.AreEqual (Int32.MinValue, byteReader.ReadInt ()))

                (byteWriter.WriteUInt32 (UInt32.MaxValue), fun () -> Assert.AreEqual (UInt32.MaxValue, byteReader.ReadUInt32 ()))
                (byteWriter.WriteUInt32 (UInt32.MinValue), fun () -> Assert.AreEqual (UInt32.MinValue, byteReader.ReadUInt32 ()))

                (byteWriter.WriteSingle (5.388572987598298734987f), fun () -> Assert.AreEqual (5.388572987598298734987f, byteReader.ReadSingle ()))
            ]

        byteStream.Position <- 0

        ops
        |> List.iter (fun (_, test) ->
            test ()
        )
