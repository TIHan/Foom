namespace Foom.Ecs

open System
open System.Collections.Generic
open System.Collections.Concurrent

type ESystemState =
    {
        Name: string
        Queues: ResizeArray<obj>
    }

type ESystemContext<'Update> =
    {
        ESystemState: ESystemState
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        Actions: ResizeArray<'Update -> unit>
    }

type Behavior<'Update> = Behavior of (ESystemContext<'Update> -> unit)

type ESystem<'Update> =
    {
        State: ESystemState
        CreateContext: EntityManager -> EventAggregator -> ESystemContext<'Update>
        Behavior: Behavior<'Update> list
    }

[<RequireQualifiedAccess>]
module Behavior =

    let eventQueue (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        Behavior (fun context ->
            let queue = ConcurrentQueue<'T> ()
            context.EventAggregator.GetEvent<'T>().Publish.Add queue.Enqueue

            (fun updateData ->
                let mutable item = Unchecked.defaultof<#IEvent>
                while queue.TryDequeue (&item) do
                    f item updateData context.EntityManager
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

[<RequireQualifiedAccess>]
module ESystem =

    let create name behavior =
        let state =
            {
                Name = name
                Queues = ResizeArray ()
            }

        {
            State = state
            CreateContext =
                fun entityManager eventManager ->
                    {
                        ESystemState = state
                        EntityManager = entityManager
                        EventAggregator = eventManager
                        Actions = ResizeArray ()
                    }
            Behavior = behavior
        }
