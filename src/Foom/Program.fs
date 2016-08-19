open System
open System.IO
open System.Diagnostics
open System.Numerics
open System.Threading.Tasks

open Foom.Client
open Foom.Ecs
open Foom.Ecs.World
open Foom.Common.Components

[<Literal>]
let DoomUnit = 64.f

let world = World (65536)

let start (invoke: Task ref) =
    let client = Client.init (world)

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
        (fun t ->
            //world.EntityManager.TryFind<CameraComponent> (fun _ _ -> true)
            //|> Option.iter (fun (ent, cameraComp) ->
            //    world.EntityManager.TryGet<TransformComponent> (ent)
            //    |> Option.iter (fun (transformComp) ->

            //        world.EntityManager.TryGet<CameraRotationComponent> (ent)
            //        |> Option.iter (fun cameraRotComp ->

            //            let mousePosition = Input.getMousePosition ()

            //            let x = mousePosition.XRel
            //            let y = mousePosition.YRel
            //            cameraRotComp.X <- cameraRotComp.X + (single x * -0.25f) * (float32 Math.PI / 180.f)
            //            cameraRotComp.Y <- cameraRotComp.Y + (single y * -0.25f) * (float32 Math.PI / 180.f)

            //            transformComp.Rotation <- Quaternion.CreateFromAxisAngle (Vector3.UnitX, 90.f * (float32 Math.PI / 180.f))

            //            transformComp.Rotation <- transformComp.Rotation *
            //                Quaternion.CreateFromYawPitchRoll (
            //                    cameraRotComp.X,
            //                    cameraRotComp.Y,
            //                    0.f
            //                )
            //        )
                       
            //    )
            //)

            Client.draw t client client
        )

[<EntryPoint>]
let main argv =
    start (new Task (fun () -> ()) |> ref)
    0
