namespace Foom.Ecs

open System
open System.Collections.Generic
open System.Collections.Concurrent

type EntitySystemState =
    {
        Name: string
        Queues: ResizeArray<obj>
    }

type EntitySystem<'Update> =
    {
        State: EntitySystemState
        InitEvents: EventManager -> unit
        Init: EntityManager -> EventManager -> ('Update -> unit)
    }

type EventHook = EventHook of (EntitySystemState -> EventManager -> unit)

type EventQueue<'T> =
    {
        Queue: ConcurrentQueue<'T>
        Hook': EventHook
        IsHooked: bool ref
    }

    member this.Process (f: 'T -> unit) =
        let mutable item = Unchecked.defaultof<'T>
        while this.Queue.TryDequeue (&item) do f item

    member this.Hook = this.Hook'

[<AutoOpen>]
module EntitySystem =

    let createEventQueue<'T when 'T :> IEntitySystemEvent> () =
        let queue = ConcurrentQueue<'T> ()
        let isHooked = ref false
        {
            Queue = queue
            Hook' = 
                EventHook (fun (state: EntitySystemState) (eventManager: EventManager) ->
                    if !isHooked |> not then
                        state.Queues.Add queue 
                        isHooked := true
                        eventManager.GetEvent().Publish.Add queue.Enqueue
                )
            IsHooked = isHooked
        }

    let system name (events: EventHook list) init =
        let state =
            {
                Name = name
                Queues = ResizeArray ()
            }

        {
            State = state
            InitEvents =
                fun eventManager ->
                    events
                    |> List.iter (fun (EventHook f) ->
                        f state eventManager
                    )
            Init = init
        }
