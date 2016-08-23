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
        EventManager: EventManager
        Actions: ResizeArray<'Update -> unit>
    }

type Sys<'Update> = Sys of (SysContext<'Update> -> unit)

type EntitySystem<'Update> =
    {
        State: EntitySystemState
        CreateSysContext: EntityManager -> EventManager -> SysContext<'Update>
        SysCollection: Sys<'Update> list
    }

[<AutoOpen>]
module SysOperators =

    let eventQueue (f: #IEntitySystemEvent -> 'Update -> EntityManager -> unit) = 
        Sys (fun context ->
            let queue = ConcurrentQueue<'T> ()
            context.EventManager.GetEvent<'T>().Publish.Add queue.Enqueue

            (fun updateData ->
                let mutable item = Unchecked.defaultof<#IEntitySystemEvent>
                while queue.TryDequeue (&item) do
                    f item updateData context.EntityManager
            )
            |> context.Actions.Add
        )

    let update (f: 'Update -> EntityManager -> EventManager -> unit) = 
        Sys (fun context ->
            (fun updateData ->
                f updateData context.EntityManager context.EventManager
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
                        EventManager = eventManager
                        Actions = ResizeArray ()
                    }
            SysCollection = actions
        }
