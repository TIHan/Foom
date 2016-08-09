namespace Foom.Ecs.World

open System
open System.Diagnostics
open System.Collections.Generic
open System.Threading
open Foom.Ecs

[<Sealed>]
type SystemHandle<'Update> (subscriptions: IDisposable [], updates: ('Update -> unit) [], shutdown: (unit -> unit)) =
    let mutable subscriptions = subscriptions
    let mutable updates = updates
    let mutable shutdown = shutdown
    let mutable isDisposed = ref false

    member this.Update data =
        if not !isDisposed then
            for i = 0 to updates.Length - 1 do
                let update = updates.[i]
                update data

    member this.Dispose () =
        let isDisposed = Interlocked.Exchange(&isDisposed, ref true)
        if not !isDisposed then 
            subscriptions |> Array.iter(fun s -> s.Dispose())
            shutdown ()

            shutdown <- id
            subscriptions <- [||]
            updates <- [||]

    interface IDisposable with
        member this.Dispose() =
            this.Dispose()

type Metric =
    {
        Name: string
        mutable TotalMilliseconds: float
    }

[<ReferenceEquality>]
type MetricTree =
    | Node of Metric * MetricTree list
    | Empty

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventManager.Create ()
    let entityManager = EntityManager.Create (eventManager, maxEntityAmount)

    let metricTrees = ResizeArray<MetricTree> ()

    let rec flattenUpdates (sys: IEntitySystem<'Update>) =
        match sys.Initialize entityManager eventManager with
        | Update (name, update) ->
            let metric =
                {
                    Name = name
                    TotalMilliseconds = 0.
                }
             
            let s = System.Diagnostics.Stopwatch ()

            (
                Node (metric, []),
                [

                    fun data ->
                        s.Restart ()
                        update data
                        s.Stop ()
                        metric.TotalMilliseconds <- s.Elapsed.TotalMilliseconds
                ]
            )

        | Merged (name, systems) ->
            let trees, updates =
                systems
                |> List.map (fun sys -> flattenUpdates sys)
                |> List.unzip

            let updates =
                if updates.IsEmpty then [ ]
                else
                    updates |> List.reduce (@)

            if updates.IsEmpty then (Empty, [ ])
            else
                let metric =
                    {
                        Name = name
                        TotalMilliseconds = 0.
                    }

                let metrics = 
                    trees 
                    |> List.choose (function | Node (metric, _) -> Some metric | _ -> None)
                    |> Array.ofList

                let updateMetrics data =
                    metric.TotalMilliseconds <- 0.
                    for i = 0 to metrics.Length - 1 do
                        metric.TotalMilliseconds <- metric.TotalMilliseconds + metrics.[i].TotalMilliseconds

                (Node (metric, trees), updates @ [ updateMetrics ])

        | _ -> 
            (Empty, [ ])

    member this.PrintMetrics () =
        let rec printMetric depth (tree: MetricTree) =
            match tree with
            | Empty -> ()
            | Node (metric, trees) ->
                let tabs =
                    match Array.init depth (fun _ -> "\t") with
                    | [||] -> ""
                    | xs -> Array.reduce (+) xs

                Debug.WriteLine (String.Format ("{0}| {1} - Time: {2} ms", tabs, metric.Name, metric.TotalMilliseconds))
                trees
                |> List.iter (printMetric (depth + 1))
        metricTrees
        |> Seq.iter (printMetric 0)

    member this.AddSystem<'Update> (EntitySystem sysCtor) =
        let sys = sysCtor ()
        let subs =
            sys.Events
            |> List.map (fun x -> x.Handle eventManager)
            |> List.toArray

        let metricTree, updates = flattenUpdates sys

        metricTrees.Add metricTree

        let updates = updates |> List.toArray
        let shutdown = fun () -> metricTrees.Remove metricTree |> ignore; sys.Shutdown ()

        new SystemHandle<'Update> (subs, updates, shutdown)
