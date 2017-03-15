namespace Foom.Ecs

open System
open System.Diagnostics
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices

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

    member val Owner = Entity (0, 0u) with get, set

type IEvent = interface end

[<Sealed>]
type ComponentAdded (ent: Entity, comp: Component) = 

    member val Entity = ent

    member val Component = comp

module Events =

    [<Sealed>]
    type ComponentRemoved<'T when 'T :> Component> (ent: Entity) = 

        member this.Entity = ent

        interface IEvent

    [<Sealed>]
    type AnyComponentAdded (ent: Entity, compT: Type) =

        member this.Entity = ent

        member this.ComponentType = compT

        interface IEvent

    [<Sealed>]
    type AnyComponentRemoved (ent: Entity, compT: Type) =

        member this.Entity = ent

        member this.ComponentType = compT

        interface IEvent

    [<Sealed>]
    type EntitySpawned (ent: Entity) =

        member this.Entity = ent

        interface IEvent

    [<Sealed>]
    type EntityDestroyed (ent: Entity) =

        member this.Entity = ent

        interface IEvent

open Events

[<ReferenceEquality>]
type EventAggregator  =
    {
        Lookup: ConcurrentDictionary<Type, obj>

        ComponentAddedLookup: Dictionary<Type, obj>
    }

    static member Create () =
        {
            Lookup = ConcurrentDictionary<Type, obj> ()

            ComponentAddedLookup = Dictionary ()
        }

    member this.Publish (event: 'T when 'T :> IEvent and 'T : not struct) =
        let mutable value = Unchecked.defaultof<obj>
        if this.Lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger event

    member this.GetEvent<'T when 'T :> IEvent> () =
       this.Lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj)) :?> Event<'T>

    member this.GetComponentAddedEvent (t: Type) =
        let mutable o = null
        if (this.ComponentAddedLookup.TryGetValue (t, &o)) then
            o :?> Event<ComponentAdded>
        else
            let e = Event<ComponentAdded> ()
            this.ComponentAddedLookup.[t] <- e :> obj
            e

    
