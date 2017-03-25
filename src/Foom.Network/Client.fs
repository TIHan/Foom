namespace Foom.Network

open System
open System.Collections.Generic

[<Sealed>]
type Client (udpClient: IUdpClient) =

    let typeDeserializers = Array.init 1024 (fun _ -> Event<ByteReader> ())

    let packetPool = Stack (Array.init 64 (fun _ -> Packet ()))
    let packetQueue = Queue<Packet> ()

    let mutable isConnected = false
    let connected = Event<IUdpEndPoint> ()

    member val Connected = connected.Publish

    member this.Connect (address, port) =

        if udpClient.Connect (address, port) then
            let packet = packetPool.Pop ()
            packet.SetData (PacketType.ConnectionRequested, [||], 0, 0)
            packetQueue.Enqueue packet

    member private this.OnReceivePacket (packet: Packet) =
        let reader = packet.Reader

        let rec onReceivePacket (reader : ByteReader) =
            let header = reader.Read<PacketHeader> ()

            match header.packetType with
            | PacketType.Unreliable ->

                let typeId = reader.ReadByte () |> int

                if typeDeserializers.Length > typeId then
                    typeDeserializers.[typeId].Trigger reader

            | PacketType.ConnectionAccepted ->

                isConnected <- true
                connected.Trigger (udpClient.RemoteEndPoint)

            | _ -> ()

            for i = 0 to int header.mergeCount - 1 do
                onReceivePacket reader

        onReceivePacket reader

    member private this.Receive () =

        while udpClient.IsDataAvailable do
            let packet = packetPool.Pop ()

            match udpClient.Receive (packet.Raw, 0, packet.Raw.Length) with
            | 0 -> ()
            | byteCount ->
                packet.Length <- byteCount
                this.OnReceivePacket packet

    member private this.Send () =
        while packetQueue.Count > 0 do

            let packet = packetQueue.Dequeue ()
            udpClient.Send (packet.Raw, packet.Length) |> ignore
            this.Recycle packet

    member this.Subscribe<'T when 'T : (new : unit -> 'T)> f =
        match Network.lookup.TryGetValue typeof<'T> with
        | true, (id, _, deserialize) ->
            let evt = typeDeserializers.[id]

            evt.Publish.Add (fun byteReader ->
                let msg = new 'T ()
                deserialize (msg :> obj) byteReader
                f msg
            )
        | _ -> ()

    member this.Update () =
        this.Receive ()
        this.Send ()

    member this.Recycle (packet: Packet) =
        packet.Reset ()
        packetPool.Push packet
