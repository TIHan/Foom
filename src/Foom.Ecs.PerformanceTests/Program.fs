open Foom.Ecs

open System

open Foom.Collections

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

type title =
    {
        mutable text : string
    }

type subtitle =
    {
        mutable text : string
    }

type yAxis =
    {
        mutable title : title
        mutable max : int
    }

type legend =
    {
        mutable layout : string
        mutable align : string
        mutable verticalAlign : string
    }

type plotOptionsSeries =
    {
        mutable pointStart : int
    }

type series<'T> =
    {
        mutable name : string
        data : 'T seq
    }

type plotOptions =
    {
        mutable series : plotOptionsSeries
    }

type Chart =
    {
        mutable title : title
        mutable subtitle : subtitle
        mutable yAxis : yAxis
        mutable legend : legend
        mutable plotOptions : plotOptions
        series : obj []
    }

let perfRecord title iterations f =
    let mutable total = 0.
    let data = ResizeArray ()

    GC.Collect (2, GCCollectionMode.Forced, true)

    for i = 1 to iterations do
        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
        f ()
        stopwatch.Stop ()
        data.Add stopwatch.Elapsed.TotalMilliseconds

    {
        name = title
        data = data
    }

[<EntryPoint>]
let main argv = 
    let amount = 10000
    let world = World (65536)

    let rng = Random ()
    let arr = Array.init amount (fun i -> i)
    let arrRng = Array.init amount (fun i -> rng.Next(0, amount))

    let data = Array.init amount (fun _ -> BigPositionComponent1 Unchecked.defaultof<Vector3>)
    let dataRng = Array.init amount (fun i -> data.[arrRng.[i]])

    let mutable result = Unchecked.defaultof<Vector3>

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
        for i = 0 to amount - 1 do
            if i % j = 0 then
                entities.[i]
                |> world.EntityManager.Destroy

        for i = 0 to amount - 1 do
            if i % j = 0 then
                entities.Add <| world.EntityManager.Spawn (proto ())

    let mutable count = 0
    let test5 = perfRecord "ECS Iteration Non-Cache Local" 1000 (fun () ->
        for i = 1 to 10 do
            count <- 0
            world.EntityManager.ForEach<BigPositionComponent1> (fun _ c -> 
                result <- c.Position7
                count <- count + 1
            )
    )

    printfn "%A" count

    let test6 = perfRecord "ECS Iteration Non-Cache Local - Smaller Two Comps" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<SubSystemComponent, PositionComponent> (fun _ _ c -> result <- c.Position)
    )

    let test7 = perfRecord "ECS Iteration Non-Cache Local - Smaller" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<PositionComponent> (fun _ c -> result <- c.Position)
    )

    let test8 = perfRecord "ECS Iteration Non-Cache Local - One Small + One Big" 1000 (fun () ->
        for i = 1 to 10 do
            world.EntityManager.ForEach<SubSystemComponent, BigPositionComponent1> (fun _ _ c -> result <- c.Position7)
    )

    let chartData =
        {
            title = { text = "Iteration Performance" }
            subtitle = { text = "" }
            yAxis =
                {
                    title = { text = "ms" }
                    max = 3
                }
            legend =
                {
                    layout = "vertical"
                    align = "right"
                    verticalAlign = "middle"
                }
            plotOptions =
                {
                    series = { pointStart = 0 }
                }
            series = 
                [|
                    test1
                    test2
                    test3
                    test4
                    test5
                    test6
                    test7
                    test8
                |]
        }

    let stringData = Newtonsoft.Json.JsonConvert.SerializeObject (chartData)
    System.IO.File.WriteAllText ("chart.html",
    """
<script src="https://code.highcharts.com/highcharts.js"></script>
<script src="https://code.highcharts.com/modules/exporting.js"></script>

<div id="container"></div>

<script>
Highcharts.chart('container', """ + stringData +
"""
);
</script>
    """
    )

    System.Diagnostics.Process.Start ("chart.html")

    0
