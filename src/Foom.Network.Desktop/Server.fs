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

    let lookup = Dictionary<IPAddress, EndPoint * DesktopConnectedClient> ()

    let readerms = new MemoryStream (65536)
    let reader = new BinaryReader (readerms)
    let writerms = new MemoryStream (65536)
    let writer = new BinaryWriter (writerms)

    let mutable reliableStringSequence = 0us

    interface IServer with

        member this.Start () =
            printfn "Starting server..."

           // tcp.Start ()

        member this.Stop () =
            printfn "Stopping server..."

            //tcp.Stop ()

        member this.Heartbeat () =
            writerms.Position <- 0L
            writerms.SetLength (0L)

            while udp.Available > 0 do
                readerms.Position <- 0L
                readerms.SetLength (1024L)

                let ipendpoint = IPEndPoint (IPAddress.Any, 0)
                let mutable endpoint = ipendpoint :> EndPoint
                let bytes = udp.Client.ReceiveFrom (readerms.GetBuffer (), &endpoint)
                let ipendpoint : IPEndPoint = downcast endpoint
                readerms.SetLength (int64 bytes)

                if bytes > 0 then
                    let a = reader.ReadByte ()

                    match Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<byte, ClientMessageType> (a) with
                    | ClientMessageType.ConnectionRequested -> 

                        if not <| lookup.ContainsKey ipendpoint.Address then
                            udp.Send ([| byte ServerMessageType.ConnectionEstablished |], 1, ipendpoint) |> ignore

                            let connectedClient = DesktopConnectedClient (endpoint)
                            let tup = (endpoint, connectedClient)
                            lookup.[ipendpoint.Address] <- tup

                            clientConnected.Trigger (connectedClient)

                    //| ClientMessage.ReliableString ->

                    //    match lookup.TryGetValue ipendpoint.Address with
                    //    | true, (endpoint, connectedClient) ->
                    //        let packet = DesktopPacket (buffer, 1, bytes - 1) :> IPacket
                    //        clientPacketReceived.Trigger (connectedClient :> IConnectedClient, packet)
                    //    | _ -> ()

                    | _ -> ()

        member val ClientConnected = clientConnected.Publish

        member val Received = received.Publish

        member this.BroadcastReliableString str =
            (this :> IServer).DebugBroadcastReliableString (str, reliableStringSequence)
            reliableStringSequence <- reliableStringSequence + 1us

        member this.DebugBroadcastReliableString (str, n) =
            writerms.Position <- 0L
            writerms.SetLength (0L)

            writer.Write (byte ServerMessageType.ReliableOrder)
            writer.Write (n)
            writer.Write (str)

            lookup
            |> Seq.iter (fun pair ->
                let (endpoint, connectedClient) = pair.Value
                udp.Send (writerms.GetBuffer (), int writerms.Length, endpoint :?> IPEndPoint) |> ignore
            )

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

            