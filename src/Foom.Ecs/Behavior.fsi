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

    static member ForEach<'T, 'Update when 'T :> Component> : (Entity -> 'T -> unit) -> Behavior<'Update>

[<RequireQualifiedAccess>]
module Behavior =

    val handleEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    val handleLatestEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    val handleComponentAdded : (Entity -> #Component -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    val update : ('Update -> EntityManager -> EventAggregator -> unit) -> Behavior<'Update>

    val merge : Behavior<'Update>  list -> Behavior<'Update>
