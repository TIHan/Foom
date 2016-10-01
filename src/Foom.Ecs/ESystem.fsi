namespace Foom.Ecs

open System

type internal ESystemState =
    {
        Name: string
        Queues: ResizeArray<obj>
    }

type internal ESystemContext<'Update> =
    {
        ESystemState: ESystemState
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        Actions: ResizeArray<'Update -> unit>
    }

type Behavior<'Update> = internal Behavior of (ESystemContext<'Update> -> unit)

type ESystem<'Update> =
    internal {
        State: ESystemState
        CreateContext: EntityManager -> EventAggregator -> ESystemContext<'Update>
        Behavior: Behavior<'Update> list
    }

[<RequireQualifiedAccess>]
module Behavior =

    val handleEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    val handleLatestEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    val update : ('Update -> EntityManager -> EventAggregator -> unit) -> Behavior<'Update>

[<RequireQualifiedAccess>]
module ESystem =

    val create : name: string -> behavior: Behavior<'Update> list -> ESystem<'Update>