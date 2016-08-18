namespace Foom.Ecs

open System
open System.Runtime.InteropServices

#nowarn "9"

/// Construct that is an unique identifier to the world.
[<Struct; StructLayout (LayoutKind.Explicit)>]
type Entity =

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
type IEntityComponent = interface end

/// Similar to Entity, but is typed on a specific component.
/// Can make querying for the specific component of the given entity faster (no dictionary lookup).
[<Sealed>]
type EntityRef<'T when 'T :> IEntityComponent and 'T : not struct> =

    /// The Entity that refers to EntityRef.
    member Entity : Entity

/// Defines what entities with the specified component type that Entity Manager is interested in.
/// Can make querying for the entities faster (no dictionary lookup).
[<Sealed>]
type Aspect<'T when 'T :> IEntityComponent and 'T : not struct>

/// Common events published by the Entity Manager.
module Events = 

    /// Published when a component was added to an existing entity.
    [<Sealed>]
    type ComponentAdded<'T when 'T :> IEntityComponent and 'T : not struct> =

        /// The entity the component was added to.
        member Entity : Entity

        interface IEntitySystemEvent

    /// Published when a component was removed from an exsting entity.
    [<Sealed>]
    type ComponentRemoved<'T when 'T :> IEntityComponent and 'T : not struct> = 

        /// The entity the component was removed from.
        member Entity : Entity

        interface IEntitySystemEvent

    /// Published when any component was added to an existing entity.
    [<Sealed>]
    type AnyComponentAdded = 

        /// The entity the component was added to.
        member Entity : Entity

        /// The component type.
        member ComponentType : Type

        interface IEntitySystemEvent

    /// Published when any component was removed from an existing entity.
    [<Sealed>]
    type AnyComponentRemoved =
       
        /// The entity the component was removed from.
        member Entity : Entity

        /// The component type.
        member ComponentType : Type

        interface IEntitySystemEvent

    /// Published when an entity has spawned.
    [<Sealed>]
    type EntitySpawned =

        /// The entity spawned.
        member Entity : Entity

        interface IEntitySystemEvent

    /// Published when an entity was destroyed.
    [<Sealed>]
    type EntityDestroyed =

        /// The entity destroyed.
        member Entity : Entity

        interface IEntitySystemEvent

/// Responsible for querying/adding/removing components and spawning/destroying entities.
[<Sealed>]
type EntityManager =

    static member internal Create : EventManager * maxEntityCount: int -> EntityManager

    //************************************************************************************************************************

    /// Attempts to find an EntityRef of component type 'T.
    member TryGetEntityRef<'T when 'T :> IEntityComponent and 'T : not struct> : Entity -> EntityRef<'T> option

    /// Gets an Aspect of component type 'T.
    member GetAspect<'T when 'T :> IEntityComponent and 'T : not struct> : unit -> Aspect<'T>

    //************************************************************************************************************************

    /// Attempts to find a component of type 'T based on the specified Entity.
    member TryGet<'T when 'T :> IEntityComponent and 'T : not struct> : Entity -> 'T option

    /// Attempts to find a component of type 'T based on the specified EntityRef.
    member TryGet<'T when 'T :> IEntityComponent and 'T : not struct> : EntityRef<'T> -> 'T option

    /// Attempts to find a component of type 'T based on the specified Aspect and Entity.
    member TryGet<'T when 'T :> IEntityComponent and 'T : not struct> : Aspect<'T> * Entity -> 'T option

    /// Checks to see if the Entity is valid.
    member IsValid : Entity -> bool

    /// Checks to see if the Entity is valid and has a component of type 'T.
    member HasComponent<'T when 'T :> IEntityComponent and 'T : not struct> : Entity -> bool

    //************************************************************************************************************************

    /// Iterate entities that have a component of type 'T.
    member ForEach<'T when 'T :> IEntityComponent and 'T : not struct> : (Entity -> 'T -> unit) -> unit

    /// Iterate entities that have a component of type 'T on the Aspect.
    member ForEach<'T when 'T :> IEntityComponent and 'T : not struct> : Aspect<'T> * (Entity -> 'T -> unit) -> unit

    /// Iterate entities that have components of type 'T1 and 'T2.
    member ForEach<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    /// Iterate entities that have components of type 'T1, 'T2, and 'T3.
    member ForEach<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

    /// Iterate entities that have components of type 'T1, 'T2, 'T3, and 'T4.
    member ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T4 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> : (Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit) -> unit

    /// Attempts to find a component of type 'T and its corresponding Entity based on the criteria.
    member TryFind<'T when 'T :> IEntityComponent and 'T : not struct> : predicate: (Entity -> 'T -> bool) -> (Entity * 'T) option

    /// Attempts to find a component of type 'T1 and 'T2 and its corresponding Entity based on the criteria.
    member TryFind<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> : predicate: (Entity -> 'T1 -> 'T2 -> bool) -> (Entity * 'T1 * 'T2) option

    // Components

    member AddComponent<'T when 'T :> IEntityComponent and 'T : not struct> : Entity -> 'T -> unit

    member RemoveComponent<'T when 'T :> IEntityComponent and 'T : not struct> : Entity -> unit

    // Entites

    member Spawn : unit -> Entity

    /// Defers to destroy the specified Entity.
    member Destroy : Entity -> unit
