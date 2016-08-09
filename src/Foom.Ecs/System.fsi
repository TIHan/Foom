namespace Foom.Ecs

open System

type internal EntitySystemState =
    {
        Name: string
        Queues: ResizeArray<obj>
    }

type EntitySystem<'Update> =
    internal {
        State: EntitySystemState
        InitEvents: EventManager -> unit
        Init: EntityManager -> EventManager -> ('Update -> unit)
    }

[<Sealed>]
type EventHook

[<Sealed>]
type EventQueue<'T> =

    member Process : ('T -> unit) -> unit

    member Hook : EventHook

[<AutoOpen>]
module EntitySystem =

    val createEventQueue<'T when 'T :> IEntitySystemEvent> : unit -> EventQueue<'T>

    val system : name: string -> events: EventHook list -> init: (EntityManager -> EventManager -> ('Update -> unit)) -> EntitySystem<'Update>