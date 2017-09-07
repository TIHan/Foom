namespace Foom.Ecs

open System
open System.Collections.Generic
open System.Collections.Concurrent

open Foom.Collections

type BehaviorContext<'Update> =
    {
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        mutable Update : 'Update -> unit
    }

type Behavior<'Update> = internal BehaviorUpdate of (BehaviorContext<'Update> -> unit)

[<Sealed>]
type Behavior private () =

    static member HandleEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        BehaviorUpdate (fun context ->
            let queue = ConcurrentQueue<#IEvent> ()
            context.EventAggregator.GetEvent<#IEvent>().Publish.Add queue.Enqueue

            context.Update <- (fun updateData ->
                let mutable item = Unchecked.defaultof<#IEvent>
                while queue.TryDequeue (&item) do
                    f item updateData context.EntityManager
            )
        )

    static member HandleLatestEvent (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        BehaviorUpdate (fun context ->
            let mutable latestEvent = Unchecked.defaultof<#IEvent>
            context.EventAggregator.GetEvent<#IEvent>().Publish.Add (fun x -> latestEvent <- x)

            context.Update <- (fun updateData ->
                if not <| obj.ReferenceEquals (latestEvent, null) then
                    f latestEvent updateData context.EntityManager
                    latestEvent <- Unchecked.defaultof<#IEvent>
            )
        )

    static member ComponentAdded<'T, 'Update when 'T :> Component> (f: 'Update -> Entity -> 'T -> unit) =
        BehaviorUpdate (fun context ->
            let queue = Queue<'T> ()
            context.EventAggregator.GetComponentAddedEvent<'T>().Publish.Add queue.Enqueue

            let em = context.EntityManager
            context.Update <- (fun updateData ->
                while queue.Count > 0 do
                    let item = queue.Dequeue ()
                    let ent = item.Owner
                    if em.IsValid ent then
                        f updateData ent item
            )
        )

    static member ComponentAdded<'T1, 'T2, 'Update when 'T1 :> Component and 'T2 :> Component> (f : 'Update -> Entity -> 'T1 -> 'T2 -> unit) =
        BehaviorUpdate (fun context ->
            let queue = Queue ()
            context.EventAggregator.GetComponentAddedEvent<'T1>().Publish.Add queue.Enqueue

            let em = context.EntityManager
            context.Update <- (fun updateData ->
                let mutable c2 = Unchecked.defaultof<'T2>
                while queue.Count > 0 do
                    let c1 = queue.Dequeue ()
                    let ent = c1.Owner
                    if em.TryGet<'T2> (ent, &c2) then
                        f updateData ent c1 c2

            )
        )

    static member ComponentAdded<'T1, 'T2, 'T3, 'Update when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component> (f : 'Update -> Entity -> 'T1 -> 'T2 -> 'T3 -> unit) =
        BehaviorUpdate (fun context ->
            let queue = Queue ()
            context.EventAggregator.GetComponentAddedEvent<'T1>().Publish.Add queue.Enqueue

            let em = context.EntityManager
            context.Update <- (fun updateData ->
                let mutable c2 = Unchecked.defaultof<'T2>
                let mutable c3 = Unchecked.defaultof<'T3>
                while queue.Count > 0 do
                    let c1 = queue.Dequeue ()
                    let ent = c1.Owner
                    if em.TryGet<'T2> (ent, &c2) then
                        if em.TryGet<'T3> (ent, &c3) then
                            f updateData ent c1 c2 c3

            )
        )

    static member ComponentAdded<'T1, 'T2, 'T3, 'T4, 'Update when 'T1 :> Component and 'T2 :> Component and 'T3 :> Component and 'T4 :> Component> (f : 'Update -> Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit) =
        BehaviorUpdate (fun context ->
            let queue = Queue ()
            context.EventAggregator.GetComponentAddedEvent<'T1>().Publish.Add queue.Enqueue

            let em = context.EntityManager
            context.Update <- (fun updateData ->
                let mutable c2 = Unchecked.defaultof<'T2>
                let mutable c3 = Unchecked.defaultof<'T3>
                let mutable c4 = Unchecked.defaultof<'T4>
                while queue.Count > 0 do
                    let c1 = queue.Dequeue ()
                    let ent = c1.Owner
                    if em.TryGet<'T2> (ent, &c2) then
                        if em.TryGet<'T3> (ent, &c3) then
                            if em.TryGet<'T4> (ent, &c4) then
                                f updateData ent c1 c2 c3 c4

            )
        )

    static member Update (f: 'Update -> EntityManager -> EventAggregator -> unit) = 
        BehaviorUpdate (fun context ->
            context.Update <- (fun updateData ->
                f updateData context.EntityManager context.EventAggregator
            )
        )

    static member Merge (behaviors: Behavior<'Update> list) =
        BehaviorUpdate (fun context ->
            let updates = Array.zeroCreate behaviors.Length

            behaviors
            |> List.iteri (fun i -> function
                | BehaviorUpdate f ->  
                    f context
                    updates.[i] <- context.Update
            )

            context.Update <-
                fun data ->
                    for i = 0 to updates.Length - 1 do
                        let update = updates.[i]
                        update data
        )

    static member Delay (f : unit -> Behavior<_>) =
        BehaviorUpdate (fun context ->
            match f () with
            | BehaviorUpdate f -> f context
        )
