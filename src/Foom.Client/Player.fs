namespace Foom.Client

open System
open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Geometry
open Foom.Client
open Foom.Renderer

open Foom.Game.Core

[<Sealed>]
type PlayerComponent () =
    inherit Component ()

    member val IsMovingForward = false with get, set

    member val IsMovingLeft = false with get, set

    member val IsMovingRight = false with get, set

    member val IsMovingBackward = false with get, set

    member val Yaw = 0.f with get, set

    member val Pitch = 0.f with get, set

[<RequireQualifiedAccess>]
[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Player =

    let fixedUpdate : Behavior<float32 * float32> =
        Behavior.merge
            [
                Behavior.update (fun (time, deltaTime) em ea ->
                    em.ForEach<CameraComponent, TransformComponent, PlayerComponent> (fun ent cameraComp transformComp playerComp ->

                        transformComp.TransformLerp <- transformComp.Transform

                        let mutable acc = Vector3.Zero
                        
                        if playerComp.IsMovingForward then
                            let v = Vector3.Transform (-Vector3.UnitZ, transformComp.Rotation)
                            acc <- (Vector3 (v.X, v.Y, v.Z))

                        if playerComp.IsMovingLeft then
                            let v = Vector3.Transform (-Vector3.UnitX, transformComp.Rotation)
                            acc <- acc + (Vector3 (v.X, v.Y, v.Z))

                        if playerComp.IsMovingBackward then
                            let v = Vector3.Transform (Vector3.UnitZ, transformComp.Rotation)
                            acc <- acc + (Vector3 (v.X, v.Y, v.Z))

                        if playerComp.IsMovingRight then
                            let v = Vector3.Transform (Vector3.UnitX, transformComp.Rotation)
                            acc <- acc + (Vector3 (v.X, v.Y, v.Z))
                       
                        acc <- 
                            if acc <> Vector3.Zero then
                                acc |> Vector3.Normalize |> (*) 10.f
                            else
                                acc

                        transformComp.Translate(acc)

                        let v1 = Vector2 (transformComp.Position.X, transformComp.Position.Y)
                        let v2 = Vector2 (transformComp.TransformLerp.Translation.X, transformComp.TransformLerp.Translation.Y)

                        cameraComp.HeightOffsetLerp <- cameraComp.HeightOffset
                        //cameraComp.HeightOffset <- sin(8.f * time) * (v1 - v2).Length()
                    )
                )


                Behavior.update (fun _ em ea ->
                    em.ForEach<CameraComponent, TransformComponent, PlayerComponent> (fun ent cameraComp transformComp playerComp ->

                        transformComp.Rotation <- Quaternion.CreateFromAxisAngle (Vector3.UnitX, 90.f * (float32 Math.PI / 180.f))

                        transformComp.Rotation <- transformComp.Rotation *
                            Quaternion.CreateFromYawPitchRoll (
                                playerComp.Yaw * 0.25f,
                                playerComp.Pitch * 0.25f,
                                0.f
                            )
                    )
                )
            ]

    let preUpdate (willQuit: bool ref) (app: Application) =
        Behavior.merge
            [
                Behavior.update (fun () em ea ->
                    em.ForEach<CameraComponent, TransformComponent, PlayerComponent> (fun ent cameraComp transformComp playerComp ->

                        Input.pollEvents (app.Window)
                        let inputState = Input.getState ()

                        inputState.Events
                        |> List.iter (function
                            | MouseMoved (x, y, xrel, yrel) ->
                                playerComp.Yaw <- playerComp.Yaw + (single xrel * -0.25f) * (float32 Math.PI / 180.f)
                                playerComp.Pitch <- playerComp.Pitch + (single yrel * -0.25f) * (float32 Math.PI / 180.f)

                            | KeyPressed x when x = 'w' -> playerComp.IsMovingForward <- true
                            | KeyReleased x when x = 'w' -> playerComp.IsMovingForward <- false

                            | KeyPressed x when x = 'a' -> playerComp.IsMovingLeft <- true
                            | KeyReleased x when x = 'a' -> playerComp.IsMovingLeft <- false
 
                            | KeyPressed x when x = 's' -> playerComp.IsMovingBackward <- true
                            | KeyReleased x when x = 's' -> playerComp.IsMovingBackward <- false

                            | KeyPressed x when x = 'd' -> playerComp.IsMovingRight <- true
                            | KeyReleased x when x = 'd' -> playerComp.IsMovingRight <- false

                            | KeyPressed x when x = '\027' -> willQuit := true

                            | x -> ()
                        )
                    )
                )

                Behavior.update (fun () em ea ->
                    em.ForEach<CameraComponent, TransformComponent, PlayerComponent> (fun ent cameraComp transformComp playerComp ->

                        transformComp.Rotation <- Quaternion.CreateFromAxisAngle (Vector3.UnitX, 90.f * (float32 Math.PI / 180.f))

                        transformComp.Rotation <- transformComp.Rotation *
                            Quaternion.CreateFromYawPitchRoll (
                                playerComp.Yaw * 0.25f,
                                playerComp.Pitch * 0.25f,
                                0.f
                            )
                    )
                )
            ]
