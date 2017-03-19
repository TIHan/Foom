namespace Foom.Network

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Collections.Generic

type DesktopConnectedClient (endpoint: EndPoint) =

    interface IConnectedClient with

        member this.Address = endpoint.ToString ()
       
type DesktopServer () =

    let clientConnected = Event<IConnectedClient> ()
    let received = Event<IConnectedClient * BinaryReader> ()

    let udp = new UdpClient (27015)

    let lookup = Dictionary<IPAddress, EndPoint * DesktopConnectedClient * bool [] * DateTime []> ()

    let outgoingReliableStream = new ByteStream (65536)
    let incomingReliableStream = new ByteStream (65536)

    let mutable reliableStringSequence = 0us

    let reliableQueue = Queue<OutgoingMessage> ()

    interface IServer with

        member this.Start () =
            printfn "Starting server..."

           // tcp.Start ()

        member this.Stop () =
            printfn "Stopping server..."

            //tcp.Stop ()

        member this.Heartbeat () =

            while udp.Available > 0 do
                incomingReliableStream.Position <- 0L
                incomingReliableStream.SetLength 1024L

                let ipendpoint = IPEndPoint (IPAddress.Any, 0)
                let mutable endpoint = ipendpoint :> EndPoint
                let bytes = udp.Client.ReceiveFrom (incomingReliableStream.Buffer, &endpoint)
                let ipendpoint : IPEndPoint = downcast endpoint
                incomingReliableStream.SetLength (int64 bytes)

                if bytes > 0 then
                    let a = incomingReliableStream.Reader.ReadByte ()

                    match Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<byte, ClientMessageType> (a) with
                    | ClientMessageType.ConnectionRequested -> 

                        if not <| lookup.ContainsKey ipendpoint.Address then
                            udp.Send ([| byte ServerMessageType.ConnectionEstablished |], 1, ipendpoint) |> ignore

                            let connectedClient = DesktopConnectedClient (endpoint)
                            let tup = (endpoint, connectedClient, Array.init 65536 (fun _ -> true), Array.init 65536 (fun _ -> DateTime.Now))
                            lookup.[ipendpoint.Address] <- tup
                            //udp.JoinMulticastGroup ipendpoint.Address
                            clientConnected.Trigger (connectedClient)

                    //| ClientMessage.ReliableString ->

                    //    match lookup.TryGetValue ipendpoint.Address with
                    //    | true, (endpoint, connectedClient) ->
                    //        let packet = DesktopPacket (buffer, 1, bytes - 1) :> IPacket
                    //        clientPacketReceived.Trigger (connectedClient :> IConnectedClient, packet)
                    //    | _ -> ()

                    | _ -> ()

            let dateTime = DateTime.Now
            let milli = dateTime.TimeOfDay.TotalMilliseconds
            lookup
            |> Seq.iter (fun pair ->
                let (_, _, reliable, reliableTime) = pair.Value

                for i = 0 to reliable.Length - 1 do
                    if not reliable.[i] && (milli - reliableTime.[i].TimeOfDay.TotalMilliseconds) > 500. then
                        failwith "this should fail"
            )

            outgoingReliableStream.Position <- 0L
            outgoingReliableStream.SetLength 0L

            outgoingReliableStream.Writer.Write (byte ServerMessageType.ReliableOrder)
            outgoingReliableStream.Writer.Write (reliableStringSequence)
            reliableStringSequence <- reliableStringSequence + 1us

            while reliableQueue.Count > 0 && lookup.Count > 0 do
                let msg = reliableQueue.Dequeue ()
                outgoingReliableStream.Writer.Write (msg.Stream.Buffer, 0, int msg.Stream.Length)

                lookup
                |> Seq.iter (fun pair ->
                    let (endpoint, _, _, _) = pair.Value

                    udp.Client.SendTo (outgoingReliableStream.Buffer, int outgoingReliableStream.Length, SocketFlags.None, endpoint)
                    |> ignore
                )
            reliableQueue.Clear ()

        member val ClientConnected = clientConnected.Publish

        member val Received = received.Publish

        member this.CreateMessage () =
            new OutgoingMessage ()

        member this.SendMessage (msg: OutgoingMessage) =
            reliableQueue.Enqueue msg

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            (udp :> IDisposable).Dispose ()
            (this :> IServer).Stop ()
            lookup.Clear ()

type DesktopClient () =

    let udp = new UdpClient ()
    let received = Event<BinaryReader> ()

    let packetBuffer = Array.zeroCreate<byte> 1024
    let packetStream = new MemoryStream (packetBuffer)
    let packetReader = new BinaryReader (packetStream)

    // reliable
    let mutable reliableSeqN = 0us
    let reliableStream = new MemoryStream (65536)
    let reliableReader = new BinaryReader (reliableStream)
    let reliableWriter = new BinaryWriter (reliableStream)

    let outgoingStream = new ByteStream (65536)
    let outgoingQueue = new Queue<OutgoingMessage> ()

    let reliablePositions = Array.zeroCreate<int> 65536
    let reliableExists = Array.zeroCreate<bool> 65536

    let mutable mainEndpoint = null

    interface IClient with

        member this.Connect ip =
            async {
                let address = IPAddress.Parse ip
                mainEndpoint <- IPEndPoint (address, 27015)
                udp.Connect (mainEndpoint)
                udp.Send ([| byte ClientMessageType.ConnectionRequested |], 1) |> ignore
                return true
            }

        member this.Heartbeat () =

            packetStream.SetLength (1024L)
            reliableStream.SetLength (65536L)

            let mutable startSeqN = reliableSeqN
            let mutable maxSeqN = reliableSeqN

            while udp.Available > 0 do
                packetStream.Position <- 0L

                let mutable endpoint = mainEndpoint :> EndPoint
                let bytes = udp.Client.ReceiveFrom (packetBuffer, &endpoint)

                if bytes > 0 then
                    let a = packetReader.ReadByte ()

                    match Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<byte, ServerMessageType> (a) with
                    | ServerMessageType.ReliableOrder ->

                        let seqN = packetReader.ReadUInt16 ()

                        if reliableExists.[int seqN] then
                            failwith "we have looped all the way around 65536"

                        let count = bytes - 3

                        if int reliableStream.Position + bytes >= 65536 then
                            failwith "we are over the stream, handle this later"

                        reliablePositions.[int seqN] <- int reliableStream.Position
                        reliableExists.[int seqN] <- true


                        Buffer.BlockCopy (packetBuffer, 3, reliableStream.GetBuffer (), int reliableStream.Position, count)
                        reliableStream.Position <- reliableStream.Position + int64 count


                        if seqN > maxSeqN || seqN < startSeqN then
                            maxSeqN <- seqN
                    | _ -> ()

            let mutable hasMoreData = false

            let maxN =
                if maxSeqN < startSeqN then
                    hasMoreData <- true
                    65535us
                else
                    maxSeqN

            let mutable missingPackets = false

            for i = int startSeqN to int maxN do
                let exists = reliableExists.[i]
                if exists && not missingPackets then
                    reliableExists.[i] <- false
                    let position = reliablePositions.[i]

                    reliableStream.Position <- int64 position
                    received.Trigger (reliableReader)
                    reliableSeqN <- uint16 i
                else
                    missingPackets <- true

            if hasMoreData then
                for i = 0 to int maxSeqN do
                    let exists = reliableExists.[i]
                    if exists && not missingPackets then
                        reliableExists.[i] <- false
                        let position = reliablePositions.[i]

                        reliableStream.Position <- int64 position
                        received.Trigger (reliableReader)
                        reliableSeqN <- uint16 i
                    else
                        missingPackets <- true

        member val Received = received.Publish

    interface IDisposable with

        member this.Dispose () =
            udp.Close ()
            (udp :> IDisposable).Dispose ()

            