namespace Foom.Network

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Runtime.InteropServices

open LiteNetLib
open LiteNetLib.Utils

type Writer () =

    member val DataWriter = Unchecked.defaultof<NetDataWriter> with get, set

    interface IWriter with

        member this.Put value =
            this.DataWriter.Put value

type Reader () =

    member val DataReader = Unchecked.defaultof<NetDataReader> with get, set

    interface IReader with

        member this.GetInt () =
            this.DataReader.GetInt ()

type Client () as this =

    static let mutable messagesReceivedCount = 0

    let serializer = NetSerializer ()
    let writer = Writer ()
    let reader = Reader ()

    let client = NetManager (this, "foom")
    let connected = Event<unit> ()
    let disconnected = Event<unit> ()

    let mutable isConnected = false

    do
        client.MergeEnabled <- true
        if client.Start () |> not then
            failwith "Client failed to start."

    interface INetEventListener with

        member this.OnPeerConnected peer =

            printfn "[Client] connected to: %A:%A" peer.EndPoint.Host peer.EndPoint.Port
            isConnected <- true
            connected.Trigger ()
               
        member this.OnPeerDisconnected (peer, disconnectInfo) =

            printfn "[Client] disconnected: %A" disconnectInfo.Reason
            isConnected <- false
            disconnected.Trigger ()

        member this.OnNetworkError (endpoint, socketErrorCode) =

            printfn "[Client] error! %A" socketErrorCode

        member this.OnNetworkReceive (peer, reader) =
            serializer.ReadAllPackets (reader)

        member this.OnNetworkReceiveUnconnected (remoteEndPoint, reader, messageType) =
            ()

        member this.OnNetworkLatencyUpdate (peer, latency) =
            ()

    interface IClient with

        member this.Connect (address, port) =
            client.Connect (address, port)

        member this.Disconnect () =
            for peer in client.GetPeers () do
                client.DisconnectPeer (peer)

        member this.Update () =
            client.PollEvents ()

        member val Connected = connected.Publish

        member val Disconnected = disconnected.Publish

        member this.RegisterType<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> (write: IWriter -> 'T -> unit, read: IReader -> 'T) =
            let action : Action<NetDataWriter, 'T> = 
                Action<NetDataWriter, 'T> (fun dataWriter data -> 
                    writer.DataWriter <- dataWriter
                    write writer data
                )
            let func : Func<NetDataReader, 'T> =
                Func<NetDataReader, 'T> (fun dataReader ->
                    reader.DataReader <- dataReader
                    read reader
                )
            serializer.RegisterCustomType (action, func)

        member this.Subscribe<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> f =
            serializer.Subscribe<'T> (Action<'T> (f))

    interface IDisposable with
        
        member this.Dispose () =
            client.Stop ()

type Server (maxConnections) as this =

    let serializer = NetSerializer ()
    let dataWriter = NetDataWriter (true)
    let writer = Writer ()
    let reader = Reader ()

    let server = NetManager (this, maxConnections, "foom")
    let clientConnected = Event<unit> ()
    let clientDisconnected = Event<unit> ()

    interface INetEventListener with

        member this.OnPeerConnected peer =

            printfn "[Server] Peer connected: %A" peer.EndPoint
            let peers = server.GetPeers ()
            for netPeer in peers do
                printfn "ConnectedPeersList: id=%A, ep=%A" netPeer.ConnectId netPeer.EndPoint

            clientConnected.Trigger ()

        member this.OnPeerDisconnected (peer, disconnectInfo) =

            printfn "[Server] Peer disconnected: %A, reason: %A" peer.EndPoint disconnectInfo.Reason

            clientDisconnected.Trigger ()

        member this.OnNetworkError (endPoint, socketErrorCode) =

            printfn "[Server] error: %A" socketErrorCode

        member this.OnNetworkReceive (peer, reader) =
            serializer.ReadAllPackets (reader)

        member this.OnNetworkReceiveUnconnected (remoteEndPoint, reader, messageType) =

            printfn "[Server] ReceiveUnconnected: %A" <| reader.GetString(100)

        member this.OnNetworkLatencyUpdate (peer, latency) =
            ()


    interface IServer with

        member this.Start port =
            server.Start port

        member this.Stop () =
            server.Stop ()

        member this.Update () =
            dataWriter.Reset ()
            server.PollEvents ()

        member val ClientConnected = clientConnected.Publish

        member val ClientDisconnected = clientDisconnected.Publish

        member this.SendToAll<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> (data: 'T) =
            serializer.Serialize<'T> (dataWriter, data)
            server.SendToAll (dataWriter, SendOptions.ReliableOrdered)

        member this.RegisterType<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> (write: IWriter -> 'T -> unit, read: IReader -> 'T) =
            let action : Action<NetDataWriter, 'T> = 
                Action<NetDataWriter, 'T> (fun dataWriter data -> 
                    writer.DataWriter <- dataWriter
                    write writer data
                )
            let func : Func<NetDataReader, 'T> =
                Func<NetDataReader, 'T> (fun dataReader ->
                    reader.DataReader <- dataReader
                    read reader
                )
            serializer.RegisterCustomType (action, func)

        member this.Subscribe<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> f =
            serializer.Subscribe<'T> (Action<'T> (f))

    interface IDisposable with

        member this.Dispose () =
            server.Stop ()

[<Sealed>]
type UdpEndPoint (ipEndPoint: IPEndPoint) =
        
    member this.IPEndPoint = ipEndPoint

    interface IUdpEndPoint with

        member this.IPAddress = ipEndPoint.Address.ToString ()

[<AbstractClass>]
type Udp =

    val UdpClient : UdpClient

    val UdpClientV6 : UdpClient

    new () =
        let udpClient = new UdpClient ()
        let udpClientV6 = new UdpClient ()

        { UdpClient = udpClient; UdpClientV6 = udpClientV6 }

    new (port) =
        let udpClient = new UdpClient (port, AddressFamily.InterNetwork)
        let udpClientV6 = new UdpClient (port, AddressFamily.InterNetworkV6)

        { UdpClient = udpClient; UdpClientV6 = udpClientV6 }

    interface IUdp with

        member this.IsDataAvailable = 
            this.UdpClient.Available > 0 || this.UdpClientV6.Available > 0

        member this.Close () =
            this.UdpClient.Close ()
            this.UdpClientV6.Close ()

    interface IDisposable with

        member this.Dispose () =
            (this :> IUdp).Close ()
            (this.UdpClient :> IDisposable).Dispose ()
            (this.UdpClientV6 :> IDisposable).Dispose ()

[<Sealed>]
type UdpClient () =
    inherit Udp ()

    let mutable isConnected = false
    let mutable isIpV6 = false

    interface IUdpClient with
       
        member this.Connect (address, port) =
            match IPAddress.TryParse (address) with
            | true, ipAddress -> 
                if ipAddress.AddressFamily = AddressFamily.InterNetwork then
                    this.UdpClient.Connect (ipAddress, port)
                    isConnected <- true
                    isIpV6 <- false
                    true
                elif ipAddress.AddressFamily = AddressFamily.InterNetworkV6 then
                    this.UdpClientV6.Connect (ipAddress, port)
                    isConnected <- true
                    isIpV6 <- true
                    true
                else
                    false
            | _ ->
                if address.ToLower () = "localhost" then
                    try
                        this.UdpClientV6.Connect (IPAddress.IPv6Loopback, port)
                        isConnected <- true
                        isIpV6 <- true
                    with | _ ->
                        this.UdpClient.Connect (IPAddress.Loopback, port)
                        isConnected <- true
                        isIpV6 <- false
                    true
                else
                    false

        member this.RemoteEndPoint =
            if not isConnected then
                failwith "Remote End Point is invalid because we haven't tried to connect."

            if isIpV6 then
                UdpEndPoint (this.UdpClientV6.Client.RemoteEndPoint :?> IPEndPoint) :> IUdpEndPoint
            else
                UdpEndPoint (this.UdpClient.Client.RemoteEndPoint :?> IPEndPoint) :> IUdpEndPoint

        member this.Receive (buffer, size) =
            if not isConnected then
                failwith "Receive is invalid because we haven't tried to connect."

            let ipEndPoint = IPEndPoint (IPAddress.Any, 0)
            let mutable endPoint = ipEndPoint :> EndPoint

            match this.UdpClient.Client.ReceiveFrom (buffer, 0, size, SocketFlags.None, &endPoint) with
            | 0 ->

                match this.UdpClientV6.Client.ReceiveFrom (buffer, 0, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount -> byteCount

            | byteCount -> byteCount

        member this.Send (buffer, size) =
            if not isConnected then
                failwith "Send is invalid because we haven't tried to connect."
 
            if isIpV6 then
                this.UdpClientV6.Send (buffer, size)
            else
                this.UdpClient.Send (buffer, size)

[<Sealed>]
type UdpServer (port) =
    inherit Udp (port)

    interface IUdpServer with

        member this.Receive (buffer, size, [<Out>] remoteEP: byref<IUdpEndPoint>) =
            let ipEndPoint = IPEndPoint (IPAddress.Any, 0)
            let mutable endPoint = ipEndPoint :> EndPoint

            match this.UdpClient.Client.ReceiveFrom (buffer, 0, size, SocketFlags.None, &endPoint) with
            | 0 ->

                match this.UdpClientV6.Client.ReceiveFrom (buffer, 0, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount ->
                    remoteEP <- UdpEndPoint (endPoint :?> IPEndPoint)
                    byteCount

            | byteCount ->
                remoteEP <- UdpEndPoint (endPoint :?> IPEndPoint)
                byteCount

        member this.Send (buffer, size, remoteEP) =
            match remoteEP with
            | :? UdpEndPoint as remoteEP -> 
                if remoteEP.IPEndPoint.AddressFamily = AddressFamily.InterNetwork then
                    this.UdpClient.Send (buffer, size, remoteEP.IPEndPoint)
                elif remoteEP.IPEndPoint.AddressFamily = AddressFamily.InterNetworkV6 then
                    this.UdpClientV6.Send (buffer, size, remoteEP.IPEndPoint)
                else
                    0
            | _ -> 0