open Foom.Ecs

open System

open Foom.Collections

let perf title iterations f =
    let mutable total = 0.
    GC.Collect (2, GCCollectionMode.Forced, true)
    for i = 1 to iterations do
        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
        f ()
        stopwatch.Stop ()
        total <- total + stopwatch.Elapsed.TotalMilliseconds

    printfn "%s: %A" title (total / double iterations)

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

type BigPositionComponent2 (position : Vector3) =
    inherit Component ()

    member val Position1 = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

type BigPositionComponent3 (position : Vector3) =
    inherit Component ()

    member val Position1 = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

type BigPositionComponent4 (position : Vector3) =
    inherit Component ()

    member val Position1 = position with get, set
    member val Position2 = position with get, set
    member val Position3 = position with get, set
    member val Position4 = position with get, set
    member val Position5 = position with get, set
    member val Position6 = position with get, set
    member val Position7 = position with get, set

type BigPositionComponent5 (position : Vector3) =
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
        add (BigPositionComponent2 Unchecked.defaultof<Vector3>)
        add (BigPositionComponent3 Unchecked.defaultof<Vector3>)
        add (BigPositionComponent4 Unchecked.defaultof<Vector3>)
        add (BigPositionComponent5 Unchecked.defaultof<Vector3>)
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
    let world = World (65536)

    let test1 = perfRecord "Spawn and Destroy 1000 Entities with 2 Components and 5 Big Components" 1000 (fun () ->
        for i = 0 to 1000 - 1 do
            let ent = world.EntityManager.Spawn (proto ())
            world.EntityManager.Destroy ent
    )

    //

    let systemEntities = UnsafeResizeArray.Create 65536

    let handleSubSystemComponentAdded =
        Behavior.handleComponentAdded (fun ent (_ : SubSystemComponent) () em ->
            match em.TryGet<PositionComponent> ent with
            | Some positionComp ->
                systemEntities.Add struct ({ Position = positionComp.Position }, positionComp)
            | _ -> ()
        )
        |> world.AddBehavior

    let entities = Array.init 10000 (fun _ -> world.EntityManager.Spawn (proto ()))
    handleSubSystemComponentAdded ()

    let arr = Array.init 65536 (fun _ -> BigPositionComponent1 Unchecked.defaultof<Vector3>)

    let mutable result = Unchecked.defaultof<Vector3>

    perf "Raw Iterate over 65536 Big Components" 1000 (fun () ->
        for i = 0 to 10000 - 1 do
            result <- arr.[i].Position7
    )

    perf "Iterate over 65536 Entities with 1 Big Component" 1000 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1> (fun _ c -> result <- c.Position7)
    )

    perf "Iterate over 65536 Entities with 2 Big Components" 1000 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1, BigPositionComponent2> (fun _ _ c -> result <- c.Position7)
    )

    perf "Iterate over 65536 Entities with 3 Big Components" 1000 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1, BigPositionComponent2, BigPositionComponent3> (fun _ _ _ c -> result <- c.Position7)
    )

    let test2 = perfRecord "Iterate over 65536 Entities with 4 Big Components" 1000 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1, BigPositionComponent2, BigPositionComponent3, BigPositionComponent4> (fun _ _ _ _ c -> result <- c.Position7)
    )

    //

    //for i = 0 to 2 do
    //    let rng = Random ()
    //    for i = 0 to systemEntities.Count - 1 do
    //        systemEntities.Buffer.[i] <- systemEntities.Buffer.[rng.Next(0, 65536)]

    //    perf "Set Construct Position" 1000 (fun () ->
    //        for i = 0 to systemEntities.Count - 1 do
    //            let struct (construct, comp) = systemEntities.Buffer.[i]
    //            construct.Position <- comp.Position
    //    )

    //    for i = 0 to systemEntities.Count - 1 do
    //        systemEntities.Buffer.[i] <- systemEntities.Buffer.[rng.Next(0, 65536)]

    //    perf "Set Component Position" 1000 (fun () ->
    //        for i = 0 to systemEntities.Count - 1 do
    //            let struct (construct, comp) = systemEntities.Buffer.[i]
    //            comp.Position <- construct.Position
    //    )

    //    for i = 0 to 10 do
    //        world.EntityManager.ForEach<PositionComponent> (fun _ _ -> ())

    //entities
    //|> Array.iter (fun ent -> world.EntityManager.Destroy ent)

    //for i = 1 to 10 do
    //    let entities = Array.init 1000 (fun _ -> world.EntityManager.Spawn (proto ()))

    //    perf "HandleSubSystemComponentAdded" 1 (fun () ->
    //        handleSubSystemComponentAdded ()
    //    )

    //    entities
    //    |> Array.iter (fun ent -> world.EntityManager.Destroy ent)

    //        name: 'Installation',
    //    data: [43934, 52503, 57177, 69658, 97031, 119931, 137133, 154175]
    //}, {
    //    name: 'Manufacturing',
    //    data: [24916, 24064, 29742, 29851, 32490, 30282, 38121, 40434]
    //}, {
    //    name: 'Sales & Distribution',
    //    data: [11744, 17722, 16005, 19771, 20185, 24377, 32147, 39387]
    //}, {
    //    name: 'Project Development',
    //    data: [null, null, 7988, 12169, 15112, 22452, 34400, 34227]
    //}, {
        //name: 'Other',
        //data: [12908, 5948, 8105, 11248, 8989, 11816, 18274, 18111]
    let chartData =
        {
            title = { text = "Solar Employment" }
            subtitle = { text = "Source: thesolarfoundation.com" }
            yAxis =
                {
                    title = { text = "Number of Employees" }
                    max = 5
                }
            legend =
                {
                    layout = "vertical"
                    align = "right"
                    verticalAlign = "middle"
                }
            plotOptions =
                {
                    series = { pointStart = 2010 }
                }
            series = 
                [|
                    test1
                    test2
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
