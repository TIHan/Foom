namespace Foom.Ecs

open System
open System.Collections.Generic
open System.Collections.Concurrent

type BehaviorContext<'Update> =
    {
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        Actions: ResizeArray<'Update -> unit>
    }

type Behavior<'Update> = internal Behavior of (BehaviorContext<'Update> -> unit)

[<RequireQualifiedAccess>]
module Behavior =

    let handleEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        Behavior (fun context ->
            let queue = ConcurrentQueue<#IEvent> ()
            context.EventAggregator.GetEvent<#IEvent>().Publish.Add queue.Enqueue

            (fun updateData ->
                let mutable item = Unchecked.defaultof<#IEvent>
                while queue.TryDequeue (&item) do
                    f item updateData context.EntityManager
            )
            |> context.Actions.Add
        )

    let handleLatestEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        Behavior (fun context ->
            let mutable latestEvent = Unchecked.defaultof<#IEvent>
            context.EventAggregator.GetEvent<#IEvent>().Publish.Add (fun x -> latestEvent <- x)

            (fun updateData ->
                if not <| obj.ReferenceEquals (latestEvent, null) then
                    f latestEvent updateData context.EntityManager
                    latestEvent <- Unchecked.defaultof<#IEvent>
            )
            |> context.Actions.Add
        )

    let handleComponentAdded<'T, 'Update when 'T :> Component> (f: Entity -> 'T -> 'Update -> EntityManager -> unit) =
        Behavior (fun context ->
            let queue = ConcurrentQueue<ComponentAdded<'T>> ()
            context.EventAggregator.GetComponentAddedEvent().Publish.Add queue.Enqueue

            (fun updateData ->
                let mutable item = Unchecked.defaultof<ComponentAdded<'T>>
                while queue.TryDequeue (&item) do
                    if context.EntityManager.IsValid item.Entity then
                        f item.Entity (item.Component) updateData context.EntityManager
            )
            |> context.Actions.Add
        )

    let update (f: 'Update -> EntityManager -> EventAggregator -> unit) = 
        Behavior (fun context ->
            (fun updateData ->
                f updateData context.EntityManager context.EventAggregator
            )
            |> context.Actions.Add
        )

    let merge (behaviors: Behavior<'Update> list) =
        Behavior (fun context ->
            behaviors
            |> List.iter (function
                | Behavior f ->  f context
            )
        )
