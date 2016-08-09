namespace Foom.Ecs

open System

[<AbstractClass>]
type internal EntitySystemEvent =

    abstract internal Handle : EventManager -> IDisposable

type internal InitializeResult<'Update> =
    | Update of name: string * ('Update -> unit)
    | Merged of name: string * IEntitySystem<'Update> list
    | NoResult

and internal IEntitySystem<'Update> =

    abstract Events : EntitySystemEvent list

    abstract Shutdown : unit -> unit

    abstract Initialize : EntityManager -> EventManager -> InitializeResult<'Update>

/// Construct that is responsible for behaviour.
type EntitySystem<'Update> = internal EntitySystem of (unit -> IEntitySystem<'Update>)

///// Contains functions that help compose Entity Systems.
//[<RequireQualifiedAccess>]
//module EntitySystem =

//    /// Creates an Entity System that will execute the given lambda lazily on initialization.
//    /// This is useful for expressing Entity Systems that have their own state.
//    val build : (unit -> EntitySystem<'Update>) -> EntitySystem<'Update>

//    /// Takes a list of Entity Systems and merges them into one.
//    val merge : name: string -> EntitySystem<'Update> list -> EntitySystem<'Update>

/// Contains Entity System primitives to build more complex Entity Systems.
module Systems =

    /// Basic system that performs an update.
    /// Partially applies the given lambda on initialization before the update function gets evaluated.
    val system : name: string -> update: (EntityManager -> EventManager -> ('Update -> unit)) -> EntitySystem<'Update>

    /// Event Listener system that handles the specified event immediately.
    /// This should only be used in a handful of cases.
    //val eventListener<'Update, 'Event when 'Event :> IEntitySystemEvent and 'Event : not struct> : ('Event -> unit) -> EntitySystem<'Update>

    /// Event Queue system that handles the specified event by placing it into a queue.
    /// The queue will be processed on update. Thread safe.
    /// Partially applies the given lambda on initialization before the update function gets evaluated.
    //val eventQueue<'Update, 'Event when 'Event :> IEntitySystemEvent and 'Event : not struct> : (EntityManager -> EventManager -> ('Update -> 'Event -> unit)) -> EntitySystem<'Update>

    /// Shutdown system that executes the given lambda when a system has been called to shutdown.
    //val shutdown<'Update> : (unit -> unit) -> EntitySystem<'Update>
