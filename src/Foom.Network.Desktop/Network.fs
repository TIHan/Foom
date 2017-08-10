namespace Foom.Network

open System
open System.IO
open System.Security.Cryptography
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Runtime.InteropServices

type AesEncryption () =

    let aes = new AesCryptoServiceProvider ()
    let defaultKey = Array.zeroCreate 16
    let defaultIV = Array.zeroCreate 16

    do
        aes.Key <- defaultKey
        aes.IV <- defaultIV
        aes.Mode <- CipherMode.CBC
        aes.Padding <- PaddingMode.PKCS7

    member this.Encrypt (bytes, offset, count, output, outputOffset, outputMaxCount, key) =
        use encryptor = aes.CreateEncryptor (key, defaultIV)
        use outputStream = new MemoryStream (output, outputOffset, outputMaxCount)
        use stream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write)
        stream.Write (bytes, offset, count)
        stream.FlushFinalBlock ()
        int (outputStream.Position - int64 outputOffset)

    member this.Decrypt (bytes, offset, count, output, outputOffset, outputMaxCount, key) =
        use decryptor = aes.CreateDecryptor (key, defaultIV)
        use inputStream = new MemoryStream (bytes, offset, count)
        use stream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read)
        stream.Read (output, outputOffset, outputMaxCount)

    interface INetworkEncryption with

        member this.Encrypt (bytes, offset, count, output, outputOffset, outputMaxCount) =
            this.Encrypt (bytes, offset, count, output, outputOffset, outputMaxCount, defaultKey)

        member this.Decrypt (bytes, offset, count, output, outputOffset, outputMaxCount) =
            this.Decrypt (bytes, offset, count, output, outputOffset, outputMaxCount, defaultKey)

    interface IDisposable with

        member this.Dispose () =
            aes.Dispose ()

type UdpEndPoint =
    {
        ipEndPoint : IPEndPoint
    }

    interface IUdpEndPoint with

        member this.IPAddress = this.ipEndPoint.Address.ToString ()

[<RequireQualifiedAccess>]
module UdpConstants =

    [<Literal>]
    let DefaultReceiveBufferSize = 645120

    [<Literal>]
    let DefaultSendBufferSize = 645120

[<AbstractClass>]
type Udp =

    val UdpClient : UdpClient

    val UdpClientV6 : UdpClient

    val Buffer : byte []

    val mutable receiveBufferSize : int

    val mutable sendBufferSize : int

    val Encryption : INetworkEncryption

    new () =
        let udpClient = new UdpClient (AddressFamily.InterNetwork)
        let udpClientV6 = new UdpClient (AddressFamily.InterNetworkV6)

        udpClient.Client.Blocking <- false
        udpClientV6.Client.Blocking <- false

        udpClient.Client.ReceiveBufferSize <- UdpConstants.DefaultReceiveBufferSize
        udpClientV6.Client.ReceiveBufferSize <- UdpConstants.DefaultReceiveBufferSize
        udpClient.Client.SendBufferSize <- UdpConstants.DefaultSendBufferSize
        udpClientV6.Client.SendBufferSize <- UdpConstants.DefaultSendBufferSize

        let buffer = Array.zeroCreate 65536

        { 
            UdpClient = udpClient
            UdpClientV6 = udpClientV6
            Buffer = Array.zeroCreate 65536
            receiveBufferSize = UdpConstants.DefaultReceiveBufferSize
            sendBufferSize = UdpConstants.DefaultSendBufferSize
            Encryption = new AesEncryption ()
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

        let buffer = Array.zeroCreate 65536

        { 
            UdpClient = udpClient
            UdpClientV6 = udpClientV6
            Buffer = Array.zeroCreate 65536
            receiveBufferSize = UdpConstants.DefaultReceiveBufferSize
            sendBufferSize = UdpConstants.DefaultSendBufferSize
            Encryption = new AesEncryption ()
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

        member this.Disconnect () =
            try
                this.UdpClient.Client.Disconnect true
                this.UdpClientV6.Client.Disconnect true
            with | _ -> ()
            isConnected <- false

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

        member this.Receive (packet : Packet) =
            if not isConnected then
                failwith "Receive is invalid because we haven't tried to connect."

            let buffer = this.Buffer

            let byteCount = (this :> IUdpClient).Receive (buffer, 0, buffer.Length)
            if byteCount > 0 then
                let length = this.Encryption.Decrypt (buffer, 0, byteCount, packet.Raw, 0, packet.Raw.Length)
                packet.SetLength (int64 length)
            packet.Position <- 0L
            byteCount

        member this.Send (packet) =
            if not isConnected then
                failwith "Send is invalid because we haven't tried to connect."
 
            if isIpV6 then
                this.UdpClientV6.Send (packet.Raw, int packet.Length) |> ignore
            else
                this.UdpClient.Send (packet.Raw, int packet.Length) |> ignore

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

            let buffer = this.Buffer
            let bufferLength = this.Encryption.Encrypt (packet.Raw, 0, int packet.Length, buffer, 0, buffer.Length)

            match remoteEP with
            | :? UdpEndPoint as remoteEP -> 

                if remoteEP.ipEndPoint.AddressFamily = AddressFamily.InterNetwork then
                    let actualSize = 
                        if (this :> IUdpServer).CanForceDataLoss || (dataLossEveryOtherCall && (this :> IUdpServer).CanForceDataLossEveryOtherCall) then
                            0
                        else
                            this.UdpClient.Send (buffer, bufferLength, remoteEP.ipEndPoint)

                    bytesSentSinceLastCall <- bytesSentSinceLastCall + actualSize
                    dataLossEveryOtherCall <- not dataLossEveryOtherCall

                elif remoteEP.ipEndPoint.AddressFamily = AddressFamily.InterNetworkV6 then
                    let actualSize = 
                        if (this :> IUdpServer).CanForceDataLoss || (dataLossEveryOtherCall && (this :> IUdpServer).CanForceDataLossEveryOtherCall) then
                            0
                        else
                            this.UdpClientV6.Send (buffer, bufferLength, remoteEP.ipEndPoint)

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