namespace Foom.Ecs

open System
open Foom.Ecs

[<Sealed>]
type Subworld =

    member AddBehavior<'Update> : Behavior<'Update> -> ('Update -> unit)

    member DestroyEntities : unit -> unit

    member SpawnEntity : unit -> Entity

/// The world of the Entity Component System.
[<Sealed>]
type World =

    /// Constructor that accepts the maximum number of entities allowed in the world.
    new : maxEntityAmount: int -> World
   
    /// Adds an Entity System to the world and returns a handle.
    member AddBehavior<'Update> : Behavior<'Update> -> ('Update -> unit)

    member CreateSubworld : unit -> Subworld

    member SpawnEntity : unit -> Entity

    member Publish<'Event when 'Event :> IEvent and 'Event : not struct> : 'Event -> unit

    member EntityManager : EntityManager

    member EventAggregator : EventAggregator
