namespace Foom.Ecs

open System
open System.Collections.Generic
open System.Collections.Concurrent

open Foom.Collections

type BehaviorContext<'Update> =
    {
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        Actions: ResizeArray<'Update -> unit>
    }

type Behavior<'Update> = internal BehaviorUpdate of (BehaviorContext<'Update> -> unit)

[<Sealed>]
type Behavior private () =

    static member HandleEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        BehaviorUpdate (fun context ->
            let queue = ConcurrentQueue<#IEvent> ()
            context.EventAggregator.GetEvent<#IEvent>().Publish.Add queue.Enqueue

            (fun updateData ->
                let mutable item = Unchecked.defaultof<#IEvent>
                while queue.TryDequeue (&item) do
                    f item updateData context.EntityManager
            )
            |> context.Actions.Add
        )

    static member HandleLatestEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        BehaviorUpdate (fun context ->
            let mutable latestEvent = Unchecked.defaultof<#IEvent>
            context.EventAggregator.GetEvent<#IEvent>().Publish.Add (fun x -> latestEvent <- x)

            (fun updateData ->
                if not <| obj.ReferenceEquals (latestEvent, null) then
                    f latestEvent updateData context.EntityManager
                    latestEvent <- Unchecked.defaultof<#IEvent>
            )
            |> context.Actions.Add
        )

    static member HandleComponentAdded<'T, 'Update when 'T :> Component> (f: Entity -> 'T -> 'Update -> EntityManager -> unit) =
        BehaviorUpdate (fun context ->
            let queue = Queue<'T> ()
            context.EventAggregator.GetComponentAddedEvent<'T>().Publish.Add queue.Enqueue

            let em = context.EntityManager
            (fun updateData ->
                while queue.Count > 0 do
                    let item = queue.Dequeue ()
                    if em.IsValid item.Owner then
                        f item.Owner item updateData em
            )
            |> context.Actions.Add
        )

    static member ComponentAdded<'T1, 'T2, 'Update when 'T1 :> Component and 'T2 :> Component> (f : 'Update -> Entity -> 'T1 -> 'T2 -> unit) =
        BehaviorUpdate (fun context ->
            let queue = Queue ()
            context.EventAggregator.GetComponentAddedEvent<'T1>().Publish.Add queue.Enqueue

            let em = context.EntityManager
            (fun updateData ->
                let mutable c2 = Unchecked.defaultof<'T2>
                while queue.Count > 0 do
                    let c1 = queue.Dequeue ()
                    if em.TryGet<'T2> (c1.Owner, &c2) then
                        f updateData c1.Owner c1 c2

            )
            |> context.Actions.Add
        )

    static member Update (f: 'Update -> EntityManager -> EventAggregator -> unit) = 
        BehaviorUpdate (fun context ->
            (fun updateData ->
                f updateData context.EntityManager context.EventAggregator
            )
            |> context.Actions.Add
        )

    static member Merge (behaviors: Behavior<'Update> list) =
        BehaviorUpdate (fun context ->
            behaviors
            |> List.iter (function
                | BehaviorUpdate f ->  f context
            )
        )
