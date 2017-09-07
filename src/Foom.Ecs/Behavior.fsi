namespace Foom.Ecs

open System

type internal BehaviorContext<'Update> =
    {
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        mutable Update : 'Update -> unit
    }

type Behavior<'Update> = internal BehaviorUpdate of (BehaviorContext<'Update> -> unit)

[<Sealed>]
type Behavior =

    static member HandleEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    static member HandleLatestEvent : (#IEvent -> 'Update -> EntityManager -> unit) -> Behavior<'Update>

    static member ComponentAdded<'T, 'Update when 'T :> Component> : ('Update -> Entity -> 'T -> unit) -> Behavior<'Update>

    static member ComponentAdded<'T1, 'T2, 'Update when 'T1 :> Component and 'T2 :> Component> : ('Update -> Entity -> 'T1 -> 'T2 -> unit) -> Behavior<'Update>

    static member ComponentAdded<'T1, 'T2, 'T3, 'Update when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component> : ('Update -> Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> Behavior<'Update>

    static member ComponentAdded<'T1, 'T2, 'T3, 'T4, 'Update when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component and 'T4 :> Component> : ('Update -> Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit) -> Behavior<'Update>

    static member Update : ('Update -> EntityManager -> EventAggregator -> unit) -> Behavior<'Update>

    static member Merge : Behavior<'Update> list -> Behavior<'Update>

    static member Delay : (unit -> Behavior<'Update>) -> Behavior<'Update>

[<RequireQualifiedAccess>]
module Behavior =

    val contramap : ('T -> 'U) -> Behavior<'U> -> Behavior<'T>
