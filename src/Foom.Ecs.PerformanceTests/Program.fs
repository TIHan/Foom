open Foom.Ecs

open System
open System.Collections.Generic

open Foom.Collections

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
        max: 3
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

type PositionComponent (position : Vector3) =
    inherit Component ()

    member val Position = position with get, set

type SubSystemComponent () =
    inherit Component ()

    member val Position = Unchecked.defaultof<Vector3> with get, set

type BigPositionComponent1 (position : Vector3) =
    inherit Component ()

    member val Position1 = position with get, set
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

[<EntryPoint>]
let main argv = 
    let amount = 10000
    let world = World (65536)

    let mutable result = Unchecked.defaultof<Vector3>

    let rng = Random ()
    let arr = Array.init amount (fun i -> i)
    let arrRng = Array.init amount (fun i -> rng.Next(0, amount))

    let data = Array.init amount (fun _ -> BigPositionComponent1 Unchecked.defaultof<Vector3>)
    let dataRng = Array.init amount (fun i -> data.[arrRng.[i]])



    let test1 = perfRecord "Cache Local" 1000 (fun () ->
        for i = 1 to 10 do
            for i = 0 to data.Length - 1 do
                result <- data.[i].Position7
    )

    let test2 = perfRecord "Cache Local Index" 1000 (fun () ->
        for i = 1 to 10 do
            for i = 0 to data.Length - 1 do
                result <- data.[arr.[i]].Position7
    )

    let test3 = perfRecord "Non-Cache Local Index" 1000 (fun () ->
        for i = 1 to 10 do
            for i = 0 to data.Length - 1 do
                result <- data.[arrRng.[i]].Position7
    )

    let test4 = perfRecord "Non-Cache Local" 1000 (fun () ->
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

    let test5 = perfRecord "ECS Iteration Non-Cache Local" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<BigPositionComponent1> (fun _ c -> 
                result <- c.Position7
            )
    )

    let test6 = perfRecord "ECS Iteration Non-Cache Local - Smaller Two Comps" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<SubSystemComponent, PositionComponent> (fun (_ : Entity) _ c -> result <- c.Position)
    )

    let test7 = perfRecord "ECS Iteration Non-Cache Local - Smaller" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<PositionComponent> (fun _ c -> result <- c.Position)
    )

    let test8 = perfRecord "ECS Iteration Non-Cache Local - One Small + One Big" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<SubSystemComponent, BigPositionComponent1> (fun (_ : Entity) _ c -> result <- c.Position7)
    )

    let test9 = perfRecord "ECS Iteration Non-Cache Local - One Small + One Big - Reverse" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<BigPositionComponent1, SubSystemComponent> (fun (_ : Entity) c _ -> result <- c.Position7)
    )

    let test10 = perfRecord "ECS Iteration Non-Cache Local - One Small + One Big - No Entity" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEachNoEntity<SubSystemComponent, BigPositionComponent1> (fun _ c -> result <- c.Position7)
    )



    let series =
        [|
            test1
            test2
            test3
            test4
            test5
            test6
            test7
            test8
            test9
            test10
        |]
    System.IO.File.WriteAllText ("chart.html", createChart "Iteration Performance" series)

    System.Diagnostics.Process.Start ("chart.html") |> ignore

    0
