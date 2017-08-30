open Foom.Ecs

open System

let perf title iterations f =
    let mutable total = 0.

    for i = 1 to iterations do
        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
        f ()
        stopwatch.Stop ()
        total <- total + stopwatch.Elapsed.TotalMilliseconds
        GC.Collect ()
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

[<EntryPoint>]
let main argv = 
    let world = World (65536)

    perf "Spawn and Destroy 1000 Entities with 2 Components and 5 Big Components" 100 (fun () ->
        for i = 0 to 1000 - 1 do
            let ent = world.EntityManager.Spawn (proto ())
            world.EntityManager.Destroy ent
    )

    for i = 0 to 65536 - 1 do
        let ent = world.EntityManager.Spawn (proto ())
        ()

    perf "Iterate over 65536 Entities with 1 Big Component" 100 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1> (fun _ _ -> ())
    )

    perf "Iterate over 65536 Entities with 2 Big Components" 100 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1, BigPositionComponent2> (fun _ _ _ -> ())
    )

    perf "Iterate over 65536 Entities with 3 Big Components" 100 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1, BigPositionComponent2, BigPositionComponent3> (fun _ _ _ _ -> ())
    )

    perf "Iterate over 65536 Entities with 4 Big Components" 100 (fun () ->
        world.EntityManager.ForEach<BigPositionComponent1, BigPositionComponent2, BigPositionComponent3, BigPositionComponent4> (fun _ _ _ _ _ -> ())
    )

    0
