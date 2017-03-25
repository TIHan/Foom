namespace Foom.Network

open System
open System.Collections.Generic

type PacketPool (poolAmount) =

    let pool = Stack (Array.init poolAmount (fun _ -> Packet ()))

    member this.Amount = poolAmount

    member this.Get () = pool.Pop ()

    member this.Recycle (packet : Packet) =
        packet.Reset ()
        if pool.Count + 1 > poolAmount then
            failwith "For right now, this throws an exception" 
        pool.Push packet
