namespace Foom.Network

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Runtime.InteropServices

type UdpEndPoint =
    {
        ipEndPoint : IPEndPoint
    }

    interface IUdpEndPoint with

        member this.IPAddress = this.ipEndPoint.Address.ToString ()

[<RequireQualifiedAccess>]
module UdpConstants =

    [<Literal>]
    let DefaultReceiveBufferSize = 64512

    [<Literal>]
    let DefaultSendBufferSize = 64512

[<AbstractClass>]
type Udp =

    val UdpClient : UdpClient

    val UdpClientV6 : UdpClient

    val Buffer : byte []

    val mutable receiveBufferSize : int

    val mutable sendBufferSize : int

    new () =
        let udpClient = new UdpClient (AddressFamily.InterNetwork)
        let udpClientV6 = new UdpClient (AddressFamily.InterNetworkV6)

        udpClient.Client.Blocking <- false
        udpClientV6.Client.Blocking <- false

        udpClient.Client.ReceiveBufferSize <- UdpConstants.DefaultReceiveBufferSize
        udpClientV6.Client.ReceiveBufferSize <- UdpConstants.DefaultReceiveBufferSize
        udpClient.Client.SendBufferSize <- UdpConstants.DefaultSendBufferSize
        udpClientV6.Client.SendBufferSize <- UdpConstants.DefaultSendBufferSize

        { 
            UdpClient = udpClient
            UdpClientV6 = udpClientV6
            Buffer = Array.zeroCreate 65536
            receiveBufferSize = UdpConstants.DefaultReceiveBufferSize
            sendBufferSize = UdpConstants.DefaultSendBufferSize
        }

    new (port) =
        let udpClient = new UdpClient (port, AddressFamily.InterNetwork)
        let udpClientV6 = new UdpClient (port, AddressFamily.InterNetworkV6)

        udpClient.Client.Blocking <- false
        udpClientV6.Client.Blocking <- false

        udpClient.Client.ReceiveBufferSize <- UdpConstants.DefaultReceiveBufferSize
        udpClientV6.Client.ReceiveBufferSize <- UdpConstants.DefaultReceiveBufferSize
        udpClient.Client.SendBufferSize <- UdpConstants.DefaultSendBufferSize
        udpClientV6.Client.SendBufferSize <- UdpConstants.DefaultSendBufferSize

        { 
            UdpClient = udpClient
            UdpClientV6 = udpClientV6
            Buffer = Array.zeroCreate 65536
            receiveBufferSize = UdpConstants.DefaultReceiveBufferSize
            sendBufferSize = UdpConstants.DefaultSendBufferSize
        }

    interface IUdp with

        member this.IsDataAvailable = 
            this.UdpClient.Available > 0 || this.UdpClientV6.Available > 0

        member this.ReceiveBufferSize
            with get () = this.receiveBufferSize
            and set value =
                this.receiveBufferSize <- value
                this.UdpClient.Client.ReceiveBufferSize <- value
                this.UdpClientV6.Client.ReceiveBufferSize <- value

        member this.SendBufferSize
            with get () = this.sendBufferSize
            and set value =
                this.sendBufferSize <- value
                this.UdpClient.Client.SendBufferSize <- value
                this.UdpClientV6.Client.SendBufferSize <- value

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
                { ipEndPoint = this.UdpClientV6.Client.RemoteEndPoint :?> IPEndPoint } :> IUdpEndPoint
            else
                { ipEndPoint = this.UdpClient.Client.RemoteEndPoint :?> IPEndPoint } :> IUdpEndPoint

        member this.Receive (buffer, offset, size) =
            if not isConnected then
                failwith "Receive is invalid because we haven't tried to connect."

            if this.UdpClient.Available > 0 then

                let ipEndPoint = IPEndPoint (IPAddress.Any, 0)
                let mutable endPoint = ipEndPoint :> EndPoint

                match this.UdpClient.Client.ReceiveFrom (buffer, offset, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount ->
                    byteCount

            elif this.UdpClientV6.Available > 0 then

                let ipEndPoint = IPEndPoint (IPAddress.IPv6Any, 0)
                let mutable endPoint = ipEndPoint :> EndPoint

                match this.UdpClientV6.Client.ReceiveFrom (buffer, offset, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount ->
                    byteCount

            else 0

        member this.Receive (stream : Stream) =
            let byteCount = (this :> IUdpClient).Receive (this.Buffer, 0, this.Buffer.Length)
            if byteCount > 0 then
                stream.Position <- 0L
                stream.Write (this.Buffer, 0, byteCount)
            byteCount

        member this.Send (packet) =
            if not isConnected then
                failwith "Send is invalid because we haven't tried to connect."
 
            if isIpV6 then
                this.UdpClientV6.Send (packet.Raw, packet.Length) |> ignore
            else
                this.UdpClient.Send (packet.Raw, packet.Length) |> ignore

[<Sealed>]
type UdpServer (port) =
    inherit Udp (port)

    let mutable bytesSentSinceLastCall = 0

    let mutable dataLossEveryOtherCall = false

    interface IUdpServer with

        member this.Receive (buffer, offset, size, [<Out>] remoteEP: byref<IUdpEndPoint>) =

            if this.UdpClient.Available > 0 then

                let ipEndPoint = IPEndPoint (IPAddress.Any, 0)
                let mutable endPoint = ipEndPoint :> EndPoint

                match this.UdpClient.Client.ReceiveFrom (buffer, offset, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount ->
                    remoteEP <- { ipEndPoint = endPoint :?> IPEndPoint }
                    byteCount

            elif this.UdpClientV6.Available > 0 then

                let ipEndPoint = IPEndPoint (IPAddress.IPv6Any, 0)
                let mutable endPoint = ipEndPoint :> EndPoint

                match this.UdpClientV6.Client.ReceiveFrom (buffer, offset, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount ->
                    remoteEP <- { ipEndPoint = endPoint :?> IPEndPoint }
                    byteCount

            else 0

        member this.Send (packet : Packet, remoteEP) =
            match remoteEP with
            | :? UdpEndPoint as remoteEP -> 

                if remoteEP.ipEndPoint.AddressFamily = AddressFamily.InterNetwork then
                    let actualSize = 
                        if (this :> IUdpServer).CanForceDataLoss || (dataLossEveryOtherCall && (this :> IUdpServer).CanForceDataLossEveryOtherCall) then
                            0
                        else
                            this.UdpClient.Send (packet.Raw, packet.Length, remoteEP.ipEndPoint)

                    bytesSentSinceLastCall <- bytesSentSinceLastCall + actualSize
                    dataLossEveryOtherCall <- not dataLossEveryOtherCall

                elif remoteEP.ipEndPoint.AddressFamily = AddressFamily.InterNetworkV6 then
                    let actualSize = 
                        if (this :> IUdpServer).CanForceDataLoss || (dataLossEveryOtherCall && (this :> IUdpServer).CanForceDataLossEveryOtherCall) then
                            0
                        else
                            this.UdpClientV6.Send (packet.Raw, packet.Length, remoteEP.ipEndPoint)

                    dataLossEveryOtherCall <- not dataLossEveryOtherCall
                    bytesSentSinceLastCall <- bytesSentSinceLastCall + actualSize

            | _ -> ()

        member this.Receive (stream : Stream, endPoint) =
            let byteCount = (this :> IUdpServer).Receive (this.Buffer, 0, this.Buffer.Length, &endPoint)
            if byteCount > 0 then
                stream.Position <- 0L
                stream.Write (this.Buffer, 0, byteCount)
            byteCount

        member this.BytesSentSinceLastCall () =
            let count = bytesSentSinceLastCall
            bytesSentSinceLastCall <- 0
            count

        member val CanForceDataLoss = false with get, set

        member val CanForceDataLossEveryOtherCall = false with get, set