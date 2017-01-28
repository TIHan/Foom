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
    let client = Client.init (world)

    let stopwatch = System.Diagnostics.Stopwatch ()

    GameLoop.start 30.
        client.AlwaysUpdate
        (fun time interval ->
            stopwatch.Reset ()
            stopwatch.Start ()

            System.Threading.Thread.Sleep(1)
            GC.Collect (0)

            (!invoke).RunSynchronously ()
            invoke := (new Task (fun () -> ()))

            client.Update (
                TimeSpan.FromTicks(time).TotalSeconds |> single, 
                TimeSpan.FromTicks(interval).TotalSeconds |> single
            )

        )
        (fun currentTime t ->
            Client.draw (TimeSpan.FromTicks(currentTime).TotalSeconds |> single) t client client

            if stopwatch.IsRunning then
                stopwatch.Stop ()

                printfn "FPS: %A" (int (1000. / stopwatch.Elapsed.TotalMilliseconds))
        )

[<EntryPoint>]
let main argv =
    start (new Task (fun () -> ()) |> ref)
    0
