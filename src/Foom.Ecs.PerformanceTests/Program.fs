open Foom.Ecs

open System
open System.Collections.Generic

open Foom.Collections
//open Newtonsoft.Json.JsonReader

type seriesItem<'T> =
    {
        name : string
        data : 'T seq
    }

let createChart title (series : (string * 'T seq) seq) =

    let stringSeries = Newtonsoft.Json.JsonConvert.SerializeObject (series |> Seq.map (fun (name, data) -> { name = name; data = data }))
    sprintf """
<script src="https://code.highcharts.com/highcharts.js"></script>
<script src="https://code.highcharts.com/modules/exporting.js"></script>

<div id="container"></div>

<script>
Highcharts.chart('container', {

    title: {
        text: '%s'
    },

    yAxis: {
        title: {
            text: 'ms'
        },
        min: 0
    },
    legend: {
        layout: 'vertical',
        align: 'right',
        verticalAlign: 'middle'
    },

    series: %s

});
</script>
    """ title stringSeries

[<Struct>]
type Vector3 =
    {
        mutable X : float32
        mutable Y : float32
        mutable Z : float32
    }
 
type SubSystemConstruct =
    {
        mutable Position : Vector3
    }

[<Struct>]
type NetworkState =
    {
        mutable Position : Vector3
        mutable Position2 : Vector3
        mutable Position3 : Vector3
        mutable Position4 : Vector3
        mutable Position5 : Vector3
        mutable Position6 : Vector3
        mutable Position7 : Vector3
    }

type NetworkComponent () =
    inherit Component ()

    [<DefaultValue>]
    val mutable state : NetworkState

type PositionComponent (position : Vector3) =
    inherit Component ()

    member val Position = position with get, set

type SubSystemComponent () =
    inherit Component ()

    member val Position = Unchecked.defaultof<Vector3> with get, set

type BigPositionComponent1 (position : Vector3) =
    inherit Component ()

    member val Position = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

type BigPositionComponent2 (position : Vector3) =
    inherit Component ()

    member val Position = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

type BigPositionComponent3 (position : Vector3) =
    inherit Component ()

    member val Position = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

type BigPositionComponent4 (position : Vector3) =
    inherit Component ()

    member val Position = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

type BigPositionComponent5 (position : Vector3) =
    inherit Component ()

    member val Position = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

let proto () =
    entity {
        add (PositionComponent Unchecked.defaultof<Vector3>)
        add (SubSystemComponent ())
        add (BigPositionComponent1 Unchecked.defaultof<Vector3>)
        add (BigPositionComponent2 Unchecked.defaultof<Vector3>)
        add (BigPositionComponent3 Unchecked.defaultof<Vector3>)
        add (BigPositionComponent4 Unchecked.defaultof<Vector3>)
        add (BigPositionComponent5 Unchecked.defaultof<Vector3>)
        add (NetworkComponent ())
    }

let perfRecord title iterations f : (string * float seq) =
    let mutable total = 0.
    let data = ResizeArray ()

    GC.Collect (2, GCCollectionMode.Forced, true)

    for i = 1 to iterations do
        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
        f ()
        stopwatch.Stop ()
        data.Add stopwatch.Elapsed.TotalMilliseconds

    (title, data :> IEnumerable<float>)

let perfRecordSpecial title iterations s f e : (string * float seq) =
    let mutable total = 0.
    let data = ResizeArray ()

    GC.Collect (2, GCCollectionMode.Forced, true)

    for i = 1 to iterations do
        s ()

        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
        f ()
        stopwatch.Stop ()
        data.Add stopwatch.Elapsed.TotalMilliseconds

        e ()

    (title, data :> IEnumerable<float>)

[<EntryPoint>]
let main argv = 
    let amount = 10000
    let iterations = 100
    let world = World (65536)

    let mutable result = Unchecked.defaultof<Vector3>

    let rng = Random ()
    let arr = Array.init amount (fun i -> i)
    let arrRng = Array.init amount (fun i -> rng.Next(0, amount))

    let data = Array.init amount (fun _ -> BigPositionComponent1 Unchecked.defaultof<Vector3>)
    let dataRng = Array.init amount (fun i -> data.[arrRng.[i]])



    let test1 = perfRecord "Cache Local" iterations (fun () ->
        for i = 1 to 10 do
            for i = 0 to data.Length - 1 do
                result <- data.[i].Position7
    )

    let test2 = perfRecord "Cache Local Index" iterations (fun () ->
        for i = 1 to 10 do
            for i = 0 to data.Length - 1 do
                result <- data.[arr.[i]].Position7
    )

    let test3 = perfRecord "Non-Cache Local Index" iterations (fun () ->
        for i = 1 to 10 do
            for i = 0 to data.Length - 1 do
                result <- data.[arrRng.[i]].Position7
    )

    let test4 = perfRecord "Non-Cache Local" iterations (fun () ->
        for i = 1 to 10 do
            for i = 0 to dataRng.Length - 1 do
                result <- dataRng.[i].Position7
    )

    let entities = ResizeArray ()
    for i = 0 to amount - 1 do
        entities.Add <| world.EntityManager.Spawn (proto ())

    for j = 2 to 10 do
        world.EntityManager.ForEach<BigPositionComponent1> (fun ent _ ->
            if ent.Index % j = 0 then
                world.EntityManager.Destroy ent
        )

        for i = 1 to amount do
            if i % j = 0 then
                entities.Add <| world.EntityManager.Spawn (proto ())

    let test5 = perfRecord "ECS Iteration Non-Cache Local" iterations (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<BigPositionComponent1> (fun _ c -> 
                result <- c.Position7
            )
    )

    let test6 = perfRecord "ECS Iteration Non-Cache Local - Smaller Two Comps" iterations (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<SubSystemComponent, PositionComponent> (fun (_ : Entity) _ c -> result <- c.Position)
    )

    let test7 = perfRecord "ECS Iteration Non-Cache Local - Smaller" iterations (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<PositionComponent> (fun _ c -> result <- c.Position)
    )

    let test8 = perfRecord "ECS Iteration Non-Cache Local - One Small + One Big" iterations (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<SubSystemComponent, BigPositionComponent1> (fun (_ : Entity) _ c -> result <- c.Position7)
    )

    let test9 = perfRecord "ECS Iteration Non-Cache Local - One Small + One Big - Reverse" iterations (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<BigPositionComponent1, SubSystemComponent> (fun (_ : Entity) c _ -> result <- c.Position7)
    )

    world.DestroyAllEntities ()

    let handleComponentAdded =
        Behavior.ComponentAdded (fun _ ent (c : BigPositionComponent1) (p : PositionComponent) ->
            result <- c.Position7
        )
        |> world.AddBehavior

    let test11 = 
        perfRecordSpecial "Handle Component Added" iterations
            (fun () ->
                for i = 1 to 1000 do
                    world.EntityManager.Spawn (proto ()) |> ignore
            )
            (fun () ->
               handleComponentAdded ()
            )
            (fun () ->
                world.DestroyAllEntities ()
            )
        
    let test11 =
        let (title, data) = test11
        (title, data |> Seq.map (fun x -> x * 10.))

    let componentAddedBehaviors =
        Array.init 10 (fun _ ->
            Behavior.ComponentAdded (fun _ ent (c : BigPositionComponent1) (p : PositionComponent) ->
                result <- c.Position7
            )
            |> world.AddBehavior
        )

    let test12 = 
        perfRecordSpecial "Spawn 1000 Ents" iterations
            (fun () -> ()
            )
            (fun () ->
                for i = 1 to 1000 do
                    world.EntityManager.Spawn (proto ()) |> ignore
            )
            (fun () ->
                world.DestroyAllEntities ()

                handleComponentAdded ()
                componentAddedBehaviors
                |> Array.iter (fun f -> f ())
            )
        
    let test12 =
        let (title, data) = test12
        (title, data |> Seq.map (fun x -> x * 10.))

    let mutable json = ""
    let test13 = 
        perfRecordSpecial "Save 10000 Ents" 2
            (fun () ->
                for i = 1 to 10000 do
                    world.EntityManager.Spawn (proto ()) |> ignore
            )
            (fun () ->
                json <- world.EntityManager.Save ()
            )
            (fun () ->
                world.DestroyAllEntities ()

                handleComponentAdded ()
                componentAddedBehaviors
                |> Array.iter (fun f -> f ())
            )

    System.IO.File.WriteAllText ("savegame.txt", json)

    let test14 =
        perfRecordSpecial "Network State Iteration" iterations
            (fun () ->
                for i = 1 to 10000 do
                    world.EntityManager.Spawn (proto ()) |> ignore
            )
            (fun () ->
                world.EntityManager.ForEach<BigPositionComponent1, BigPositionComponent2, BigPositionComponent3, NetworkComponent> (fun _ c1 c2 c3 net ->
                    net.state.Position <- c1.Position
                    net.state.Position2 <- c1.Position2
                    net.state.Position3 <- c1.Position3
                    net.state.Position4 <- c2.Position4
                    net.state.Position5 <- c2.Position5
                    net.state.Position6 <- c3.Position6
                    net.state.Position7 <- c3.Position7
                )
            )
            (fun () ->
                world.DestroyAllEntities ()

                handleComponentAdded ()
                componentAddedBehaviors
                |> Array.iter (fun f -> f ())
            )

    let series =
        [|
            //test1
            //test2
            //test3
            //test4
            //test5
            //test6
            //test7
           // test8
           // test9
            test11
            test12
            test13
            test14
        |]
    System.IO.File.WriteAllText ("chart.html", createChart "Iteration Performance" series)

    System.Diagnostics.Process.Start ("chart.html") |> ignore

    0
