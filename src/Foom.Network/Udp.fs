namespace Foom.Network

open System
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

    abstract Receive : byte [] * offset: int * size: int -> int

    abstract Send : byte[] * size: int -> int

    abstract RemoteEndPoint : IUdpEndPoint

type IUdpServer =
    inherit IUdp

    abstract Receive : byte [] * offset: int * size: int * [<Out>] remoteEP: byref<IUdpEndPoint> -> int

    abstract Send : byte [] * size: int * IUdpEndPoint -> int
