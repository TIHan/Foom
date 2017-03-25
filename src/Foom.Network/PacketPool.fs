namespace Foom.Network

open System
open System.Collections.Generic

type PacketPool (poolAmount) =

    let pool = Stack (Array.init poolAmount (fun _ -> Packet ()))

    member this.Amount = poolAmount

    member this.Get () = pool.Pop ()

    member this.Recycle (packet : Packet) =
        packet.Reset ()
        pool.Push packet
