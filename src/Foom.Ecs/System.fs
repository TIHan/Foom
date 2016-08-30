namespace Foom.Ecs

open System
open System.Collections.Generic
open System.Collections.Concurrent

type EntitySystemState =
    {
        Name: string
        Queues: ResizeArray<obj>
    }

type SysContext<'Update> =
    {
        EntitySystemState: EntitySystemState
        EntityManager: EntityManager
        EventAggregator: EventAggregator
        Actions: ResizeArray<'Update -> unit>
    }

type Sys<'Update> = Sys of (SysContext<'Update> -> unit)

type EntitySystem<'Update> =
    {
        State: EntitySystemState
        CreateSysContext: EntityManager -> EventAggregator -> SysContext<'Update>
        SysCollection: Sys<'Update> list
    }

[<AutoOpen>]
module SysOperators =

    let eventQueue (f: #IEvent -> 'Update -> EntityManager -> unit) = 
        Sys (fun context ->
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
        Sys (fun context ->
            (fun updateData ->
                f updateData context.EntityManager context.EventAggregator
            )
            |> context.Actions.Add
        )

[<RequireQualifiedAccess>]
module EntitySystem =

    let create name actions =
        let state =
            {
                Name = name
                Queues = ResizeArray ()
            }

        {
            State = state
            CreateSysContext =
                fun entityManager eventManager ->
                    {
                        EntitySystemState = state
                        EntityManager = entityManager
                        EventAggregator = eventManager
                        Actions = ResizeArray ()
                    }
            SysCollection = actions
        }
