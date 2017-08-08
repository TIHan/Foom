namespace Foom.Network

open System

[<AbstractClass>]
type Peer =
    interface IDisposable

    member Connect : address : string * port : int -> unit

    member Subscribe<'T> : ('T -> unit) -> unit

    member SendUnreliable<'T> : msg : 'T -> unit

    member SendReliableOrdered<'T> : msg : 'T -> unit

    member Update : TimeSpan -> unit

    member PacketPool : PacketPool

type ServerPeer =
    inherit Peer

    member ClientConnected : IEvent<IUdpEndPoint>

    member ClientDisconnected : IEvent<IUdpEndPoint>

    member ClientPacketPoolMaxCount : int

    member ClientPacketPoolCount : int

    new : IUdpServer * connectionTimeout : TimeSpan -> ServerPeer

type ClientPeer =
    inherit Peer

    member Connected : IEvent<IUdpEndPoint>

    member Disconnected : IEvent<IUdpEndPoint>

    new : IUdpClient -> ClientPeer
