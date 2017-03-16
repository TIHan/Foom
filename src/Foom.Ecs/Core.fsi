namespace Foom.Ecs

open System
open System.Runtime.InteropServices

#nowarn "9"

/// Construct that is an unique identifier to the world.
[<Struct; StructLayout (LayoutKind.Explicit)>]
type Entity =

    internal new : int * uint32 -> Entity

    /// Internally used to index entity data in the Entity Manager.
    [<FieldOffset (0)>]
    val Index : int

    /// Version of the entity in relation to its index. 
    /// Re-using the index, will increment the version by one. Doing this repeatly, for example, 60 times a second, it will take more than two years to overflow.
    [<FieldOffset (4)>]
    val Version : uint32

    /// A union, combining index and version. Useful for logging entities.
    [<FieldOffset (0); DefaultValue>]
    val Id : uint64

    /// Checks to see if this Entity is zero-ed out.
    member IsZero : bool

/// A marker for component data.
[<AbstractClass>]
type Component =
    
    new : unit -> Component

    member internal Owner : Entity with get, set

/// A marker for event data.
type IEvent = interface end

/// Published when a component was added to an existing entity.
[<Sealed>]
type ComponentAdded<'T when 'T :> Component> =

    internal new : Entity * 'T -> ComponentAdded<'T>

    /// The entity the component was added to.
    member Entity : Entity

    member Component : 'T

/// Common events published by the Entity Manager.
module Events = 

    /// Published when a component was removed from an exsting entity.
    [<Sealed>]
    type ComponentRemoved<'T when 'T :> Component> = 

        internal new : Entity -> ComponentRemoved<'T>

        /// The entity the component was removed from.
        member Entity : Entity

        interface IEvent

    /// Published when any component was added to an existing entity.
    [<Sealed>]
    type AnyComponentAdded = 

        internal new : Entity * Type -> AnyComponentAdded

        /// The entity the component was added to.
        member Entity : Entity

        /// The component type.
        member ComponentType : Type

        interface IEvent

    /// Published when any component was removed from an existing entity.
    [<Sealed>]
    type AnyComponentRemoved =

        internal new : Entity * Type -> AnyComponentRemoved
       
        /// The entity the component was removed from.
        member Entity : Entity

        /// The component type.
        member ComponentType : Type

        interface IEvent

    /// Published when an entity has spawned.
    [<Sealed>]
    type EntitySpawned =

        internal new : Entity -> EntitySpawned

        /// The entity spawned.
        member Entity : Entity

        interface IEvent

    /// Published when an entity was destroyed.
    [<Sealed>]
    type EntityDestroyed =

        internal new : Entity -> EntityDestroyed

        /// The entity destroyed.
        member Entity : Entity

        interface IEvent

/// Responsible for publishing events.
/// Used for decoupling and communication between systems.
[<Sealed>]
type EventAggregator =

    static member internal Create : unit -> EventAggregator

    /// Publishes an event to underlying subscribers.
    member Publish<'T when 'T :> IEvent and 'T : not struct> : 'T -> unit

    member internal GetEvent<'T when 'T :> IEvent> : unit -> Event<'T>

    member internal GetComponentAddedEvent<'T when 'T :> Component> : unit -> Event<ComponentAdded<'T>>
