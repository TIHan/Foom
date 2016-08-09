namespace Foom.Ecs.World

open System
open Foom.Ecs

/// A handle of an Entity System's update function.
[<Sealed>]
type SystemHandle<'Update> =

    /// Calls the underlying Entity System's update function.
    member Update : 'Update -> unit

    /// Disposes of any subscriptions that are part of the Entity System that the handle refers to.
    /// Also calls the Shutdown function on the Entity System.
    member Dispose : unit -> unit

    interface IDisposable

/// The world of the Entity Component System.
[<Sealed>]
type World =

    /// Constructor that accepts the maximum number of entities allowed in the world.
    new : maxEntityAmount: int -> World

    /// Prints out metrics for Entity Systems that performs updates.
    member PrintMetrics : unit -> unit
   
    /// Adds an Entity System to the world and returns a handle.
    member AddSystem<'Update> : EntitySystem<'Update> -> SystemHandle<'Update>