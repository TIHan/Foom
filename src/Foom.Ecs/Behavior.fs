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

    static member ForEach<'T, 'Update when 'T :> Component> (f : Entity -> 'T -> unit) : Behavior<'Update> =
        BehaviorUpdate (fun context ->
            let lookup = Array.init context.EntityManager.MaxNumberOfEntities (fun _ -> -1)
            let entities = UnsafeResizeArray.Create 65536
            let data = UnsafeResizeArray.Create 65536

            context.EventAggregator.GetComponentAddedEvent<'T>().Publish.Add (fun comp ->
                lookup.[comp.Owner.Index] <- entities.Count
                entities.Add comp.Owner
                data.Add comp
            )

            context.EventAggregator.GetComponentRemovedEvent<'T>().Publish.Add (fun comp ->
                let entity = comp.Owner
                let index = lookup.[entity.Index]

                let swappingEntity = entities.LastItem

                lookup.[entity.Index] <- -1

                entities.SwapRemoveAt index
                data.SwapRemoveAt index

                if not (entity.Index.Equals swappingEntity.Index) then
                    lookup.[swappingEntity.Index] <- index
            )

            //let f = f context.EntityManager context.EventAggregator
            let entitiesBuffer = entities.Buffer
            let dataBuffer = data.Buffer
            (fun updateData ->
                for i = 0 to entities.Count - 1 do
                    f entitiesBuffer.[i] dataBuffer.[i]
            )
            |> context.Actions.Add

        )

[<RequireQualifiedAccess>]
module Behavior =

    let handleEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
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

    let handleLatestEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
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

    let handleComponentAdded<'T, 'Update when 'T :> Component> (f: Entity -> 'T -> 'Update -> EntityManager -> unit) =
        BehaviorUpdate (fun context ->
            let queue = ConcurrentQueue<'T> ()
            context.EventAggregator.GetComponentAddedEvent<'T>().Publish.Add queue.Enqueue

            (fun updateData ->
                let mutable item = Unchecked.defaultof<'T>
                while queue.TryDequeue (&item) do
                    if context.EntityManager.IsValid item.Owner then
                        f item.Owner item updateData context.EntityManager
            )
            |> context.Actions.Add
        )

    let update (f: 'Update -> EntityManager -> EventAggregator -> unit) = 
        BehaviorUpdate (fun context ->
            (fun updateData ->
                f updateData context.EntityManager context.EventAggregator
            )
            |> context.Actions.Add
        )

    let merge (behaviors: Behavior<'Update> list) =
        BehaviorUpdate (fun context ->
            behaviors
            |> List.iter (function
                | BehaviorUpdate f ->  f context
            )
        )
