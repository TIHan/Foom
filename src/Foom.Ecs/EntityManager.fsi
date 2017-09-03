namespace Foom.Ecs

open System
open System.Runtime.InteropServices

#nowarn "9"

[<Sealed>]
type EntityBuilder

/// Responsible for querying/adding/removing components and spawning/destroying entities.
[<Sealed>]
type EntityManager =

    static member internal Create : EventAggregator * maxEntityCount: int -> EntityManager

    //************************************************************************************************************************

    /// Attempts to find a component of type 'T based on the specified Entity.
    member TryGet<'T when 'T :> Component> : Entity -> 'T option

    member TryGet<'T when 'T :> Component> : Entity * [<Out>] comp : byref<'T> -> bool

    /// Checks to see if the Entity is valid.
    member IsValid : Entity -> bool

    /// Checks to see if the Entity is valid and has a component of type 'T.
    member Has<'T when 'T :> Component> : Entity -> bool

    //************************************************************************************************************************

    /// Iterate entities that have a component of type 'T.
    member ForEach<'T when 'T :> Component> : (Entity -> 'T -> unit) -> unit

    /// Iterate entities that have components of type 'T1 and 'T2.
    member ForEach<'T1, 'T2 when 'T1 :> Component and 'T2 :> Component> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    /// Iterate entities that have components of type 'T1, 'T2, and 'T3.
    member ForEach<'T1, 'T2, 'T3 when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

    /// Iterate entities that have components of type 'T1, 'T2, 'T3, and 'T4.
    member ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component and 'T4 :> Component> : (Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit) -> unit

    /// Attempts to find a component of type 'T and its corresponding Entity based on the criteria.
    member TryFind<'T when 'T :> Component> : predicate: (Entity -> 'T -> bool) -> (Entity * 'T) option

    /// Attempts to find a component of type 'T1 and 'T2 and its corresponding Entity based on the criteria.
    member TryFind<'T1, 'T2 when 'T1 :> Component and 'T2 :> Component> : predicate: (Entity -> 'T1 -> 'T2 -> bool) -> (Entity * 'T1 * 'T2) option

    // Components

    member Add<'T when 'T :> Component> : Entity * 'T -> unit

    // Entites

    member Spawn : unit -> Entity

    /// Defers to destroy the specified Entity.
    member Destroy : Entity -> unit

    member MaxNumberOfEntities : int

    member internal DestroyAll : unit -> unit

[<AutoOpen>]
module EntityPrototype =

    [<Struct>]
    type EntityPrototype = private EntityPrototype of (Entity -> EntityManager -> unit)

    [<Sealed>]
    type EntityPrototypeBuilder =

        member Yield : a : 'T -> EntityPrototype

        [<CustomOperation ("add", MaintainsVariableSpace = true)>]
        member AddComponent : EntityPrototype * [<ProjectionParameter>] f : (unit -> #Component) -> EntityPrototype

    val entity : EntityPrototypeBuilder
 
type EntityManager with

    member Spawn : EntityPrototype -> Entity