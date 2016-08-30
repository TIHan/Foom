namespace Foom.Ecs

open System
open System.Diagnostics
open System.Collections.Generic
open System.Threading
open Foom.Ecs

[<Sealed>]
type World (maxEntityAmount) =
    let eventAggregator = EventAggregator.Create ()
    let entityManager = EntityManager.Create (eventAggregator, maxEntityAmount)

    member this.AddESystem<'Update> (sys: ESystem<'Update>) =
        let context = sys.CreateContext entityManager eventAggregator
        sys.Behavior
        |> List.iter (fun (Behavior f) ->
            f context
        )
        fun updateData ->
            for i = 0 to context.Actions.Count - 1 do
                let f = context.Actions.[i]
                f updateData
