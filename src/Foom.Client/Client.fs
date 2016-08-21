[<RequireQualifiedAccess>]
module Foom.Client.Client

open System
open System.IO
open System.Drawing
open System.Numerics
open System.Collections.Generic

open Foom.Physics
open Foom.Renderer
open Foom.Wad
open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type ClientState = 
    {
        Window: nativeint
        Update: (float32 * float32 -> unit)
        RenderUpdate: (float32 * float32 -> unit)
    }

// These are sectors to look for and test to ensure things are working as they should.
// 568 - map10 sunder
// 4 - map10  sunder
// 4371 - map14 sunder
// 28 - e1m1 doom
// 933 - map10 sunder
// 20 - map10 sunder
// 151 - map10 sunder
// 439 - map08 sunder
// 271 - map03 sunder
// 663 - map05 sunder
// 506 - map04 sunder
// 3 - map02 sunder
// 3450 - map11 sunder
// 1558 - map11 sunder
// 240 - map07 sunder
// 2021 - map11 sunder

open Foom.Ecs
open Foom.Ecs.World
open Foom.Renderer.EntitySystem
open Foom.Common.Components
open Foom.Renderer.Components
open Foom.Level.Components

let exportFlatTextures (wad: Wad) =
    wad
    |> Wad.iterFlatTextureName (fun name ->
        Wad.tryFindFlatTexture name wad
        |> Option.iter (fun tex ->
            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let bmp = new Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb)

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iteri (fun i j pixel ->
                if pixel = Pixel.Cyan then
                    bmp.SetPixel (i, j, Color.FromArgb (0, 0, 0, 0))
                else
                    bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))
            )

            bmp.Save (tex.Name + "_flat.bmp")
            bmp.Dispose ()
        )
    )

let exportTextures (wad: Wad) =
    wad
    |> Wad.iterTextureName (fun name ->
        Wad.tryFindTexture name wad
        |> Option.iter (fun tex ->
            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let bmp = new Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb)

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iteri (fun i j pixel ->
                if pixel = Pixel.Cyan then
                    bmp.SetPixel (i, j, Color.FromArgb (0, 0, 0, 0))
                else
                    bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))
            )

            bmp.Save (tex.Name + ".bmp")
            bmp.Dispose ()
        )
    )

let spawnDoomLevelStaticGeometryMesh (geo: LevelStaticGeometry) (wad: Wad) (em: EntityManager) =
    match geo.Texture with
    | Some texture ->
            let tex = 
                if texture.IsFlat then Wad.tryFindFlatTexture texture.TextureName wad
                else Wad.tryFindTexture texture.TextureName wad
            match tex with
            | Some tex ->
                let width = Array2D.length1 tex.Data
                let height = Array2D.length2 tex.Data
                let texName = 
                    if texture.IsFlat then 
                        texture.TextureName + "_flat.bmp"
                    else 
                        texture.TextureName + ".bmp"

                let ent = em.Spawn ()
        
                em.AddComponent ent (TransformComponent (Matrix4x4.Identity))
                em.AddComponent ent (MeshComponent (geo.Vertices, texture.CreateUV width height))
                em.AddComponent ent (
                    MaterialComponent (
                        "triangle.vertex",
                        "triangle.fragment",
                        texName,
                        { R = geo.LightLevel; G = geo.LightLevel; B = geo.LightLevel; A = 0uy }
                    )
                )


            | _ -> ()

    | _ -> ()

let init (world: World) =

    // Load up doom wads.


    // Add entity system

    let app = Renderer.init ()
    let sys1 = Foom.Renderer.EntitySystem.create (app)
    let updateSys1 = world.AddSystem sys1

    let defaultPosition = Vector3 (1056.f, -3552.f, 20.f)
    let cameraEnt = world.EntityManager.Spawn ()
    world.EntityManager.AddComponent cameraEnt (CameraComponent (Matrix4x4.CreatePerspectiveFieldOfView (56.25f * 0.0174533f, ((16.f + 16.f * 0.25f) / 9.f), 16.f, System.Single.MaxValue)))
    world.EntityManager.AddComponent cameraEnt (TransformComponent (Matrix4x4.CreateTranslation (defaultPosition)))

    let physicsWorld = Physics.init ()
    let capsule = Physics.addCapsuleController defaultPosition (10.f) (36.f) physicsWorld





    let mutable isMovingForward = false
    let mutable isMovingLeft = false
    let mutable isMovingRight = false
    let mutable isMovingBackward = false

    let mutable didPreStep = false

    let clientSystem =
        EntitySystem.create "Client" 
            [
                Sys.handleLoadWadRequests (fun name -> System.IO.File.Open (name, FileMode.Open) :> Stream)

                Sys.handleWadLoaded (fun _ wad ->
                    wad |> exportFlatTextures
                    wad |> exportTextures
                )

                Sys.handleLoadLevelRequests (fun entityManager wad geo ->
                    spawnDoomLevelStaticGeometryMesh geo wad world.EntityManager
            
                    Physics.addTriangles geo.Vertices geo.Vertices.Length physicsWorld
                )

                // Initialize
                update (fun _ eventManager ->
                    eventManager.Publish (LoadWadRequested ("doom.wad"))
                    eventManager.Publish (LoadLevelRequested ("e1m1")) 
                    fun _ -> ()
                )

                update (fun entityManager eventManager (time, deltaTime) ->

                    Input.pollEvents (app.Window)
                    let inputState = Input.getState ()

                    let mutable acc = Vector3.Zero

                    world.EntityManager.TryFind<CameraComponent> (fun _ _ -> true)
                    |> Option.iter (fun (ent, cameraComp) ->
                        world.EntityManager.TryGet<TransformComponent> (ent)
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
                                acc <- (Vector3 (v.X, v.Y, 0.f))
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                //transformComp.Translate (v)

                            if isMovingLeft then
                                let v = Vector3.Transform (-Vector3.UnitX, transformComp.Rotation)
                                acc <- acc + (Vector3 (v.X, v.Y, 0.f))
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                //transformComp.Translate (v)

                            if isMovingBackward then
                                let v = Vector3.Transform (Vector3.UnitZ, transformComp.Rotation)
                                acc <- acc + (Vector3 (v.X, v.Y, 0.f))
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                //transformComp.Translate (v)

                            if isMovingRight then
                                let v = Vector3.Transform (Vector3.UnitX, transformComp.Rotation)
                                acc <- acc + (Vector3 (v.X, v.Y, 0.f))
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                //transformComp.Translate (v)
                               
                            acc <- 
                                if acc <> Vector3.Zero then
                                    acc |> Vector3.Normalize |> (*) 4.5f
                                else
                                    acc

                            //transformComp.Translate(acc)
                            Physics.setKinematicControllerWalkDirection acc capsule
                        )
                    )

                    if not didPreStep then
                        Physics.preStepKinematicController capsule physicsWorld
                        didPreStep <- true
                    Physics.stepKinematicController deltaTime capsule physicsWorld
                    Physics.step deltaTime physicsWorld

                    let position = Physics.getKinematicControllerPosition capsule

                    match entityManager.TryFind<CameraComponent, TransformComponent> (fun _ _ _ -> true) with
                    | Some (ent, cameraComp, transformComp) ->
                        transformComp.Position <- position + Vector3.UnitZ * 26.f

                        let v1 = Vector2 (transformComp.Position.X, transformComp.Position.Y)
                        let v2 = Vector2 (transformComp.TransformLerp.Translation.X, transformComp.TransformLerp.Translation.Y)
                        ()

                        cameraComp.HeightOffsetLerp <- cameraComp.HeightOffset
                        cameraComp.HeightOffset <- sin(8.f * time) * (v1 - v2).Length()
                    | _ -> ()
                )
            ]

    { 
        Window = app.Window
        Update = world.AddSystem clientSystem
        RenderUpdate = updateSys1
    }

let draw currentTime t (prev: ClientState) (curr: ClientState) =

    let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

    curr.RenderUpdate (currentTime, t)

    stopwatch.Stop ()

    //printfn "%A" stopwatch.Elapsed.TotalMilliseconds