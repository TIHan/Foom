namespace Foom.Network

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Runtime.InteropServices

[<Sealed>]
type UdpEndPoint (ipEndPoint: IPEndPoint) =
        
    member this.IPEndPoint = ipEndPoint

    interface IUdpEndPoint with

        member this.IPAddress = ipEndPoint.Address.ToString ()

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
                UdpEndPoint (this.UdpClientV6.Client.RemoteEndPoint :?> IPEndPoint) :> IUdpEndPoint
            else
                UdpEndPoint (this.UdpClient.Client.RemoteEndPoint :?> IPEndPoint) :> IUdpEndPoint

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

        member this.Receive (buffer, offset, size, [<Out>] remoteEP: byref<IUdpEndPoint>) =

            if this.UdpClient.Available > 0 then

                let ipEndPoint = IPEndPoint (IPAddress.Any, 0)
                let mutable endPoint = ipEndPoint :> EndPoint

                match this.UdpClient.Client.ReceiveFrom (buffer, offset, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount ->
                    remoteEP <- UdpEndPoint (endPoint :?> IPEndPoint)
                    byteCount

            elif this.UdpClientV6.Available > 0 then

                let ipEndPoint = IPEndPoint (IPAddress.IPv6Any, 0)
                let mutable endPoint = ipEndPoint :> EndPoint

                match this.UdpClientV6.Client.ReceiveFrom (buffer, offset, size, SocketFlags.None, &endPoint) with
                | 0 -> 0
                | byteCount ->
                    remoteEP <- UdpEndPoint (endPoint :?> IPEndPoint)
                    byteCount

            else 0

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