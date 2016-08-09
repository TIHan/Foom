namespace Foom.Ecs.World

open System
open Foom.Ecs

/// The world of the Entity Component System.
[<Sealed>]
type World =

    /// Constructor that accepts the maximum number of entities allowed in the world.
    new : maxEntityAmount: int -> World
   
    /// Adds an Entity System to the world and returns a handle.
    member AddSystem<'Update> : EntitySystem<'Update> -> ('Update -> unit)

    member EntityManager : EntityManager
