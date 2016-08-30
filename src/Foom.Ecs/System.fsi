namespace Foom.Ecs

open System

type internal EntitySystemState =
    {
        Name: string
        Queues: ResizeArray<obj>
    }

type internal SysContext<'Update> =
    {
        EntitySystemState: EntitySystemState
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        Actions: ResizeArray<'Update -> unit>
    }

type Sys<'Update> = internal Sys of (SysContext<'Update> -> unit)

type EntitySystem<'Update> =
    internal {
        State: EntitySystemState
        CreateSysContext: EntityManager -> EventAggregator -> SysContext<'Update>
        SysCollection: Sys<'Update> list
    }

[<AutoOpen>]
module SysOperators =

    val eventQueue : (#IEvent -> 'Update -> EntityManager -> unit) -> Sys<'Update>

    val update : ('Update -> EntityManager -> EventAggregator -> unit) -> Sys<'Update>

[<RequireQualifiedAccess>]
module EntitySystem =

    val create : name: string -> actions: Sys<'Update> list -> EntitySystem<'Update>