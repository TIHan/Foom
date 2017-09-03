namespace Foom.Ecs

open System

type internal BehaviorContext<'Update> =
    {
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        Actions: ResizeArray<'Update -> unit>
    }

type Behavior<'Update> = internal BehaviorUpdate of (BehaviorContext<'Update> -> unit)

[<Sealed>]
type Behavior =

    static member HandleEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    static member HandleLatestEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    static member HandleComponentAdded : (Entity -> #Component -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    static member ComponentAdded<'T1, 'T2, 'Update when 'T1 :> Component and 'T2 :> Component> : ('Update -> Entity -> 'T1 -> 'T2 -> unit) -> Behavior<'Update>

    static member Update : ('Update -> EntityManager -> EventAggregator -> unit) -> Behavior<'Update>

    static member Merge : Behavior<'Update>  list -> Behavior<'Update>
