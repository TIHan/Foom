namespace Foom.Client

open System
open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Geometry
open Foom.Client
open Foom.Common.Components
open Foom.Renderer

type PlayerComponent () =

    member val IsMovingForward = false with get, set

    member val IsMovingLeft = false with get, set

    member val IsMovingRight = false with get, set

    member val IsMovingBackward = false with get, set

    member val Yaw = 0.f with get, set

    member val Pitch = 0.f with get, set

    interface IComponent

type SkyComponent () =

    interface IComponent

[<RequireQualifiedAccess>]
module Camera =

    let playerFixedUpdate () =
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
            ]

    let playerUpdate (app: Application) =
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

                            | _ -> ()
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

                        em.TryFind<SkyComponent, TransformComponent> (fun _ _ _ -> true)
                        |> Option.iter (fun (ent, skyComp, skyTransformComp) ->
                            skyTransformComp.Transform <- transformComp.Transform
                            skyTransformComp.TransformLerp <- transformComp.TransformLerp
                        )
                    )
                )
            ]

    let update (app: Application) =
        let mutable isMovingForward = false
        let mutable isMovingLeft = false
        let mutable isMovingRight = false
        let mutable isMovingBackward = false

        Behavior.update (fun (time, deltaTime) entityManager eventManager ->

            Input.pollEvents (app.Window)
            let inputState = Input.getState ()

            let mutable acc = Vector3.Zero

            entityManager.TryFind<CameraComponent> (fun _ _ -> true)
            |> Option.iter (fun (ent, cameraComp) ->
                entityManager.TryGet<TransformComponent> (ent)
                |> Option.iter (fun (transformComp) ->

                    transformComp.TransformLerp <- transformComp.Transform
                    cameraComp.AngleLerp <- cameraComp.Angle

                    inputState.Events
                    |> List.iter (function
                        | MouseMoved (_, _, x, y) ->
                            cameraComp.AngleX <- cameraComp.AngleX + (single x * -0.25f) * (float32 Math.PI / 180.f)
                            cameraComp.AngleY <- cameraComp.AngleY + (single y * -0.25f) * (float32 Math.PI / 180.f)

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

                    transformComp.Rotation <- Quaternion.CreateFromAxisAngle (Vector3.UnitX, 90.f * (float32 Math.PI / 180.f))

                    transformComp.Rotation <- transformComp.Rotation *
                        Quaternion.CreateFromYawPitchRoll (
                            cameraComp.AngleX,
                            cameraComp.AngleY,
                            0.f
                        )

                    
                    if isMovingForward then
                        let v = Vector3.Transform (-Vector3.UnitZ, transformComp.Rotation)
                        acc <- (Vector3 (v.X, v.Y, v.Z))

                    if isMovingLeft then
                        let v = Vector3.Transform (-Vector3.UnitX, transformComp.Rotation)
                        acc <- acc + (Vector3 (v.X, v.Y, v.Z))

                    if isMovingBackward then
                        let v = Vector3.Transform (Vector3.UnitZ, transformComp.Rotation)
                        acc <- acc + (Vector3 (v.X, v.Y, v.Z))

                    if isMovingRight then
                        let v = Vector3.Transform (Vector3.UnitX, transformComp.Rotation)
                        acc <- acc + (Vector3 (v.X, v.Y, v.Z))
                        
                    acc <- 
                        if acc <> Vector3.Zero then
                            acc |> Vector3.Normalize |> (*) 700.f
                        else
                            acc

                    transformComp.Translate(acc)
                )
            )

            match entityManager.TryFind<CameraComponent, TransformComponent> (fun _ _ _ -> true) with
            | Some (ent, cameraComp, transformComp) ->
                let v1 = Vector2 (transformComp.Position.X, transformComp.Position.Y)
                let v2 = Vector2 (transformComp.TransformLerp.Translation.X, transformComp.TransformLerp.Translation.Y)

                cameraComp.HeightOffsetLerp <- cameraComp.HeightOffset
                //cameraComp.HeightOffset <- sin(8.f * time) * (v1 - v2).Length()
            | _ -> ()
        )