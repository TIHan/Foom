namespace Foom.Ecs

open System
open System.Diagnostics
open System.Collections.Generic
open System.Threading
open Foom.Ecs

[<Sealed>]
type Subworld (eventAggregator: EventAggregator, entityManager: EntityManager) =

    let entities = ResizeArray (entityManager.MaxNumberOfEntities)
    let spawnedEvent = eventAggregator.GetEntitySpawnedEvent ()
    let mutable recordEntities = false

    do
        spawnedEvent.Publish.Add (fun ent ->
            if recordEntities then
                entities.Add (ent)
        )

    member this.AddBehavior<'Update> (behav: Behavior<'Update>) =
        let context =
            {
                EntityManager = entityManager
                EventAggregator = eventAggregator
                Update = fun _ -> ()
            }

        match behav with
        | BehaviorUpdate f -> f context

        context.Update

    member this.DestroyEntities () =
        entities
        |> Seq.iter entityManager.Destroy
        entities.Clear ()

    member this.SpawnEntity () =
        let ent = entityManager.Spawn ()
        entities.Add ent
        ent

[<Sealed>]
type World (maxEntityAmount) =
    let eventAggregator = EventAggregator ()
    let entityManager = EntityManager.Create (eventAggregator, maxEntityAmount)

    member this.AddBehavior<'Update> (behav: Behavior<'Update>) =
        let context =
            {
                EntityManager = entityManager
                EventAggregator = eventAggregator
                Update = fun _ -> ()
            }

        match behav with
        | BehaviorUpdate f -> f context

        context.Update

    member this.CreateSubworld () = Subworld (eventAggregator, entityManager)

    member this.SpawnEntity () = entityManager.Spawn ()

    member this.Publish evt = eventAggregator.Publish evt

    member __.EntityManager = entityManager

    member __.EventAggregator = eventAggregator

    member __.DestroyAllEntities () = entityManager.DestroyAll ()
