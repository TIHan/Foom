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

    let mutable xpos = 0
    let mutable prevXpos = 0

    let mutable ypos = 0
    let mutable prevYpos = 0

    let mutable isMovingForward = false
    let mutable isMovingLeft = false
    let mutable isMovingRight = false
    let mutable isMovingBackward = false

    GameLoop.start 30.
        (fun time interval ->
            GC.Collect (0)

            (!invoke).RunSynchronously ()
            invoke := (new Task (fun () -> ()))

            Input.pollEvents (client.Window)
            let inputState = Input.getState ()

            world.EntityManager.TryFind<CameraComponent> (fun _ _ -> true)
            |> Option.iter (fun (ent, cameraComp) ->
                world.EntityManager.TryGet<TransformComponent> (ent)
                |> Option.iter (fun (transformComp) ->

                    transformComp.TransformLerp <- transformComp.Transform

                    world.EntityManager.TryGet<CameraRotationComponent> (ent)
                    |> Option.iter (fun cameraRotComp ->
                        cameraRotComp.AngleLerp <- cameraRotComp.Angle
                    )

                    inputState.Events
                    |> List.iter (function
                        | MouseMoved (_, _, x, y) ->

                            world.EntityManager.TryGet<CameraRotationComponent> (ent)
                            |> Option.iter (fun cameraRotComp ->
                                cameraRotComp.X <- cameraRotComp.X + (single x * -0.25f) * (float32 Math.PI / 180.f)
                                cameraRotComp.Y <- cameraRotComp.Y + (single y * -0.25f) * (float32 Math.PI / 180.f)
                            )

                        | KeyPressed x when x = 'w' -> isMovingForward <- true
                        | KeyReleased x when x = 'w' -> isMovingForward <- false

                        | KeyPressed x when x = 'a' -> isMovingLeft <- true
                        | KeyReleased x when x = 'a' -> isMovingLeft <- false

                        | KeyPressed x when x = 's' -> isMovingBackward <- true
                        | KeyReleased x when x = 's' -> isMovingBackward <- false

                        | KeyPressed x when x = 'd' -> isMovingRight <- true
                        | KeyReleased x when x = 'd' -> isMovingRight <- false

                        | _ -> ()
                    )

                    world.EntityManager.TryGet<CameraRotationComponent> (ent)
                    |> Option.iter (fun cameraRotComp ->
                        transformComp.Rotation <- Quaternion.CreateFromAxisAngle (Vector3.UnitX, 90.f * (float32 Math.PI / 180.f))

                        transformComp.Rotation <- transformComp.Rotation *
                            Quaternion.CreateFromYawPitchRoll (
                                cameraRotComp.X,
                                cameraRotComp.Y,
                                0.f
                            )
                    )

                    if isMovingForward then
                        let v = Vector3.Transform (Vector3.UnitZ * -64.f * 1.f, transformComp.Rotation)
                        transformComp.Translate (v)

                    if isMovingLeft then
                        let v = Vector3.Transform (Vector3.UnitX * -64.f * 1.f, transformComp.Rotation)
                        transformComp.Translate (v)

                    if isMovingBackward then
                        let v = Vector3.Transform (Vector3.UnitZ * 64.f * 1.f, transformComp.Rotation)
                        transformComp.Translate (v)

                    if isMovingRight then
                        let v = Vector3.Transform (Vector3.UnitX * 64.f * 1.f, transformComp.Rotation)
                        transformComp.Translate (v)
                       
                )
            )

            let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

            client.Update (TimeSpan.FromTicks(interval).TotalMilliseconds |> single)

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
