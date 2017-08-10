namespace Foom.Game.Network

open System
open System.Collections.Generic

open Foom.Ecs
open Foom.Network

type ISnapshot =

    abstract Reset : unit -> unit 

type SnapshotFactory<'T when 'T : (new : unit -> 'T) and 'T :> ISnapshot> () =

    let poolAmount = 60

    let pool = Stack (Array.init poolAmount (fun _ -> new 'T ()))

    member this.Count = pool.Count

    member this.MaxCount = poolAmount

    member this.Get () = pool.Pop ()

    member this.Recycle (packet : 'T) =
        packet.Reset ()
        if pool.Count + 1 > poolAmount then
            failwith "For right now, this throws an exception" 
        pool.Push packet

[<AbstractClass>]
type NetworkComponent<'State, 'T when 'State : struct and 'T :> Component> () =
    inherit Component ()

    abstract Map : 'T * byref<'State> -> unit

    abstract Set : byref<'State> * 'T -> unit

    abstract Serialize : ByteWriter * prev : byref<'State> * next : byref<'State> -> unit

    abstract Deserialize : byref<'State> * ByteReader -> unit

[<Sealed>]
type ServerComponent (udpServer : IUdpServer) =
    inherit Component ()

    member val Server = new Server (udpServer)

    member val Snapshot = None with get, set

    interface IDisposable with

        member this.Dispose () =
            (this.Server :> IDisposable).Dispose ()

type TestComp () =
    inherit Component ()

    member val X = 0 with get, set

    member val Y = 0 with get, set

    member val Z = 0 with get, set

[<Struct>]
type TestState =
    {
        mutable x : int
        mutable y : int
        mutable z : int
    }

type TestNetworkComp () =
    inherit NetworkComponent<TestState, TestComp> ()

    override this.Map (comp, state) =
        state.x <- comp.X
        state.y <- comp.Y
        state.z <- comp.Z

    override this.Set (state, comp) =
        comp.X <- state.x
        comp.Y <- state.y
        comp.Z <- state.z

    override this.Serialize (writer, prev, next) =
        writer.WriteDeltaInt (prev.x, next.x)
        writer.WriteDeltaInt (prev.y, next.y)
        writer.WriteDeltaInt (prev.z, next.z)

    override this.Deserialize (state, reader) =
        state.x <- reader.ReadDeltaInt (state.x)
        state.y <- reader.ReadDeltaInt (state.y)
        state.z <- reader.ReadDeltaInt (state.z)
       

