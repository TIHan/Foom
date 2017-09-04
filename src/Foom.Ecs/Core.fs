namespace Foom.Ecs

open System
open System.Diagnostics
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Runtime.Serialization

open Foom.Collections

#nowarn "9"

[<Struct; StructLayout (LayoutKind.Explicit)>]
type Entity =

    [<FieldOffset (0)>]
    val Index : int

    [<FieldOffset (4)>]
    val Version : uint32

    [<FieldOffset (0); DefaultValue>]
    val Id : uint64

    new (index, version) = { Index = index; Version = version }

    member this.IsZero = this.Id = 0UL

    override this.ToString () = String.Format ("(Entity #{0}.{1})", this.Index, this.Version)

[<AbstractClass>]
type Component () =

    [<IgnoreDataMember>]
    member val Owner = Entity (0, 0u) with get, set

type IEvent = interface end

[<Sealed>]
type EventAggregator () =

    let lookup = ConcurrentDictionary<Type, obj> ()

    let entitySpawned = Event<Entity> ()
    let entityDestroyed = Event<Entity> ()

    let componentAddedLookup = Dictionary<Type, obj * (obj -> unit)> ()
    let componentRemovedLookup = Dictionary<Type, obj> ()

    member __.Publish (event: 'T when 'T :> IEvent and 'T : not struct) =
        let mutable value = Unchecked.defaultof<obj>
        if lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger event

    member __.GetEvent<'T when 'T :> IEvent> () =
       lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj)) :?> Event<'T>

    member __.GetEntitySpawnedEvent () = entitySpawned

    member __.GetEntityDestroyedEvent () = entityDestroyed

    member __.GetComponentAddedEvent<'T when 'T :> Component> () =
        let t = typeof<'T>
        let mutable o = Unchecked.defaultof<obj * (obj -> unit)>
        if (componentAddedLookup.TryGetValue (t, &o)) then
            let (event, trigger) = o
            (event :?> Event<'T>)
        else
            let e = Event<'T> ()
            let trigger = (fun (o : obj) ->
                match o with
                | :? 'T as o -> e.Trigger o
                | _ -> ()
            )
            componentAddedLookup.[t] <- (e :> obj, trigger)
            e

    member __.GetComponentRemovedEvent<'T when 'T :> Component> () =
        let t = typeof<'T>
        let mutable o = Unchecked.defaultof<obj>
        if (componentRemovedLookup.TryGetValue (t, &o)) then
            (o :?> Event<'T>)
        else
            let e = Event<'T> ()
            componentRemovedLookup.[t] <- (e :> obj)
            e

    member __.TryGetComponentAddedTrigger (t : Type, [<Out>] trigger : byref<obj -> unit>) =
        let mutable o = Unchecked.defaultof<obj * (obj -> unit)>
        if (componentAddedLookup.TryGetValue (t, &o)) then
            let (_, trigger') = o
            trigger <- trigger'
            true
        else
            false