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
        EventManager: EventManager
        Actions: ResizeArray<'Update -> unit>
    }

type Sys<'Update> = internal Sys of (SysContext<'Update> -> unit)

type EntitySystem<'Update> =
    internal {
        State: EntitySystemState
        CreateSysContext: EntityManager -> EventManager -> SysContext<'Update>
        SysCollection: Sys<'Update> list
    }

[<AutoOpen>]
module SysOperators =

    val eventQueue : (EntityManager -> EventManager -> 'Update -> #IEntitySystemEvent -> unit) -> Sys<'Update>

    val update : (EntityManager -> EventManager -> 'Update -> unit) -> Sys<'Update>

[<RequireQualifiedAccess>]
module EntitySystem =

    val create : name: string -> actions: Sys<'Update> list -> EntitySystem<'Update>