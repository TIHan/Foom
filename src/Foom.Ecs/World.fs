namespace Foom.Ecs

open System
open System.Diagnostics
open System.Collections.Generic
open System.Threading
open Foom.Ecs

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventAggregator.Create ()
    let entityManager = EntityManager.Create (eventManager, maxEntityAmount)

    member this.AddSystem<'Update> (sys: EntitySystem<'Update>) =
        let context = sys.CreateSysContext entityManager eventManager
        sys.SysCollection
        |> List.iter (fun (Sys f) ->
            f context
        )
        fun updateData ->
            for i = 0 to context.Actions.Count - 1 do
                let f = context.Actions.[i]
                f updateData

    member this.EntityManager = entityManager
        
