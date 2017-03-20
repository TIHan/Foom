namespace Foom.Network

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Collections.Generic

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
            server.PollEvents ()

        member val ClientConnected = clientConnected.Publish

        member val ClientDisconnected = clientDisconnected.Publish

        member this.SendToAll<'T when 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> (data: 'T) =
            dataWriter.Reset ()
            serializer.Serialize<'T> (dataWriter, data)
            for peer in server.GetPeers () do
                peer.Send (dataWriter, SendOptions.Sequenced)

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
