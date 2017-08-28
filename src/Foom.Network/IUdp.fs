namespace Foom.Network

open System
open System.IO
open System.Runtime.InteropServices

type IUdpEndPoint =

    abstract IPAddress : string

type IUdp =
    inherit IDisposable

    abstract IsDataAvailable : bool

    abstract ReceiveBufferSize : int with get, set

    abstract SendBufferSize : int with get, set

    abstract Close : unit -> unit

type IUdpClient =
    inherit IUdp

    abstract Connect : address: string * port: int -> bool

    abstract Disconnect : unit -> unit

    abstract Receive : byte [] * offset: int * size: int -> int

    abstract Receive : Packet -> int

    abstract Send : Packet -> unit

    abstract RemoteEndPoint : IUdpEndPoint

type IUdpServer =
    inherit IUdp

    abstract Receive : byte [] * offset: int * size: int * [<Out>] remoteEP: byref<IUdpEndPoint> -> int

    abstract Receive : Stream * [<Out>] remoteEP: byref<IUdpEndPoint> -> int

    abstract Send : Packet * IUdpEndPoint -> unit

    abstract BytesSentSinceLastCall : unit -> int

    abstract CanForceDataLoss : bool with get, set

    abstract CanForceDataLossEveryOtherCall : bool with get, set
