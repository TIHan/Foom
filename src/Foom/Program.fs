open System
open System.IO
open System.Diagnostics
open System.Numerics

open Foom.Client
open Foom.Ecs
open Foom.Ecs.World
open Foom.Common.Components

[<Literal>]
let DoomUnit = 64.f

[<EntryPoint>]
let main argv =
    let world = World (65536)

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

            Input.pollEvents (client.Window)
            let inputState = Input.getState ()

            world.EntityManager.TryFind<CameraComponent> (fun _ _ -> true)
            |> Option.iter (fun (ent, cameraComp) ->
                world.EntityManager.TryGet<TransformComponent> (ent)
                |> Option.iter (fun (transformComp) ->

                    transformComp.TransformLerp <- transformComp.Transform

                    inputState.Events
                    |> List.iter (function
                        //| MouseMoved (_, _, x, y) ->

                        //    transformComp.ApplyYawPitchRoll (single x * -1.f, single y * -1.f, 0.f)

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

                    if isMovingForward then
                        let v = Vector3.Transform (Vector3.UnitY * 64.f * 2.f, transformComp.Rotation ())
                        transformComp.Translate (v)

                    if isMovingLeft then
                        let v = Vector3.Transform (Vector3.UnitX * -64.f * 2.f, transformComp.Rotation ())
                        transformComp.Translate (v)

                    if isMovingBackward then
                        let v = Vector3.Transform (Vector3.UnitY * -64.f * 2.f, transformComp.Rotation ())
                        transformComp.Translate (v)

                    if isMovingRight then
                        let v = Vector3.Transform (Vector3.UnitX * 64.f * 2.f, transformComp.Rotation ())
                        transformComp.Translate (v)

                )
            )
        )
        (fun t ->
            Client.draw t client client
        )
    0
