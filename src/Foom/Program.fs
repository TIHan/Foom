open System
open System.IO
open System.Diagnostics
open System.Numerics
open System.Threading.Tasks

open Foom.Client
open Foom.Ecs
open Foom.Common.Components

let world = World (65536)

let start (invoke: Task ref) =
    let client = ClientSystem.init (world)

    GameLoop.start 30.
        (fun time interval ->
            GC.Collect (0)

            (!invoke).RunSynchronously ()
            invoke := (new Task (fun () -> ()))

            let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

            client.Update (
                TimeSpan.FromTicks(time).TotalSeconds |> single, 
                TimeSpan.FromTicks(interval).TotalSeconds |> single
            )

            stopwatch.Stop ()

            printfn "%A" stopwatch.Elapsed.TotalMilliseconds
        )
        (fun currentTime t ->
            ClientSystem.draw (TimeSpan.FromTicks(currentTime).TotalSeconds |> single) t client client
        )

[<EntryPoint>]
let main argv =
    start (new Task (fun () -> ()) |> ref)
    0
