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
        Update: (float32 -> unit)
        RenderUpdate: (float32 -> unit)
        Level: Level 
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

let init (world: World) =

    // Load up doom wads.

    let doom2Wad = Wad.create (System.IO.File.Open ("doom.wad", System.IO.FileMode.Open)) |> Async.RunSynchronously
    let wad = Wad.createFromWad doom2Wad (System.IO.File.Open ("sunder.wad", System.IO.FileMode.Open)) |> Async.RunSynchronously
    let lvl = Wad.findLevel "e1m1" doom2Wad |> Async.RunSynchronously


    // Extract all doom textures.

    Wad.flats doom2Wad
    |> Array.iter (fun tex ->
        let bmp = new Bitmap(64, 64, Imaging.PixelFormat.Format32bppArgb)

        for i = 0 to 64 - 1 do
            for j = 0 to 64 - 1 do
                let pixel = tex.Pixels.[i + (j * 64)]
                bmp.SetPixel (i, j, Color.FromArgb (255, int pixel.R, int pixel.G, int pixel.B))

        bmp.Save(tex.Name + ".bmp")
        bmp.Dispose ()
    )

    Wad.flats wad
    |> Array.iter (fun tex ->
        let bmp = new Bitmap(64, 64, Imaging.PixelFormat.Format32bppArgb)

        for i = 0 to 64 - 1 do
            for j = 0 to 64 - 1 do
                let pixel = tex.Pixels.[i + (j * 64)]
                bmp.SetPixel (i, j, Color.FromArgb (255, int pixel.R, int pixel.G, int pixel.B))

        bmp.Save(tex.Name + ".bmp")
        bmp.Dispose ()
    )

    // Calculate polygons

    let sectorPolygons =
        lvl.Sectors
        //[| lvl.Sectors.[343] |]
        |> Seq.mapi (fun i s -> 
            System.Diagnostics.Debug.WriteLine ("Sector " + string i)
            (Level.createFlats i lvl, s)
        )



    // Add entity system

    let app = Renderer.init ()
    let sys1 = Foom.Renderer.EntitySystem.create (app)
    let updateSys1 = world.AddSystem sys1

    let defaultPosition = Vector3 (1568.f, -3520.f, 64.f * 3.f)
    let cameraEnt = world.EntityManager.Spawn ()
    world.EntityManager.AddComponent cameraEnt (CameraComponent ())
    world.EntityManager.AddComponent cameraEnt (TransformComponent (Matrix4x4.CreateTranslation (defaultPosition)))
    world.EntityManager.AddComponent cameraEnt (CameraRotationComponent())

    let flatUnit = 64.f

    let mutable count = 0

    lvl.Sectors
    |> Seq.iter (fun sector ->
        lvl
        |> Level.createWalls sector
        |> Seq.iter (fun renderLinedef ->

        
            match Wad.tryFindTexture renderLinedef.TextureName doom2Wad with
            | None -> ()
            | Some tex ->

            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let bmp = new Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb)

            let mutable isTransparent = false
            tex.Data
            |> Array2D.iteri (fun i j pixel ->
                if pixel = Pixel.Cyan then
                    bmp.SetPixel (i, j, Color.FromArgb (0, 0, 0, 0))
                    isTransparent <- true
                else
                    bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))
            )

            bmp.Save (tex.Name + ".bmp")
            bmp.Dispose ()

            let lightLevel = sector.LightLevel
            let lightLevel =
                if lightLevel > 255 then 255uy
                else byte lightLevel

            let ent = world.EntityManager.Spawn ()

            world.EntityManager.AddComponent ent (TransformComponent (Matrix4x4.Identity))
            world.EntityManager.AddComponent ent (MeshComponent (renderLinedef.Vertices, Wall.createUV width height renderLinedef))
            world.EntityManager.AddComponent ent (
                MaterialComponent (
                    "triangle.vertex",
                    "triangle.fragment",
                    tex.Name + ".bmp",
                    { R = lightLevel; G = lightLevel; B = lightLevel; A = 0uy },
                    isTransparent
                )
            )
        )
    )

    sectorPolygons
    |> Seq.iter (fun (polygons, sector) ->

        polygons
        |> Seq.iter (fun polygon ->
            let vertices =
                polygon.Triangles
                |> Array.map (fun x -> [|x.X;x.Y;x.Z|])
                |> Array.reduce Array.append
                |> Array.map (fun x -> Vector3 (x.X, x.Y, single sector.FloorHeight))

            let uv = Flat.createUV 64 64 polygon

            let lightLevel = sector.LightLevel
            let lightLevel =
                if lightLevel > 255 then 255uy
                else byte lightLevel

            count <- count + 1
            let ent = world.EntityManager.Spawn ()

            world.EntityManager.AddComponent ent (TransformComponent (Matrix4x4.Identity))
            world.EntityManager.AddComponent ent (MeshComponent (vertices, uv))
            world.EntityManager.AddComponent ent (
                MaterialComponent (
                    "triangle.vertex",
                    "triangle.fragment",
                    sector.FloorTextureName + ".bmp",
                    { R = lightLevel; G = lightLevel; B = lightLevel; A = 0uy },
                    false
                )
            )
        )
    )

    sectorPolygons
    |> Seq.iter (fun (polygons, sector) ->

        polygons
        |> Seq.iter (fun polygon ->
            let vertices =
                polygon.Triangles
                |> Array.map (fun x -> [|x.Z;x.Y;x.X|])
                |> Array.reduce Array.append
                |> Array.map (fun x -> Vector3 (x.X, x.Y, single sector.CeilingHeight))

            let uv = Flat.createFlippedUV 64 64 polygon

            let lightLevel = sector.LightLevel
            let lightLevel =
                if lightLevel > 255 then 255uy
                else byte lightLevel

            count <- count + 1
            let ent = world.EntityManager.Spawn ()

            world.EntityManager.AddComponent ent (TransformComponent (Matrix4x4.Identity))
            world.EntityManager.AddComponent ent (MeshComponent (vertices, uv))
            world.EntityManager.AddComponent ent (
                MaterialComponent (
                    "triangle.vertex",
                    "triangle.fragment",
                    sector.CeilingTextureName + ".bmp",
                    { R = lightLevel; G = lightLevel; B = lightLevel; A = 0uy },
                    false
                )
            )
        )
    )

    printfn "COUNT: %A" count

    let spawnBounds (bounds: BoundingBox2D) =
        let max = bounds.Max
        let min = bounds.Min
        let v1 = Vector3 (max.X, max.Y, 0.f)
        let v2 = Vector3 (min.X, max.Y, 0.f)
        let v3 = Vector3 (min.X, min.Y, 0.f)
        let v4 = Vector3 (max.X, min.Y, 0.f)

        let ent1 = world.EntityManager.Spawn ()

        world.EntityManager.AddComponent ent1 <| 
            WireframeComponent(
                [|
                    v1
                    v2

                    v2
                    v3

                    v3
                    v4

                    v4
                    v1
                |]
            )
        world.EntityManager.AddComponent ent1 <|
            MaterialComponent(
                "v.vertex", 
                "f.fragment", "", 
                { R = 255uy; G = 255uy; B = 255uy; A = 255uy },
                false
            )

    let mutable minX = 0.f
    let mutable maxX = 0.f
    let mutable minY = 0.f
    let mutable maxY = 0.f

    let mutable first = false
    sectorPolygons
    |> Seq.iter (fun (flats, _) ->

        flats
        |> Seq.iteri (fun i flat ->
            let bounds = Flat.createBoundingBox2D flat
            spawnBounds bounds
            let min = bounds.Min
            let max = bounds.Max
            if first then
                if min.X < minX then
                    minX <- min.X
                elif max.X > maxX then
                    maxX <- max.X

                if min.Y < minY then
                    minY <- min.Y
                elif max.Y > maxY then
                    maxY <- max.Y
            else
                minX <- min.X
                minY <- min.Y
                maxX <- max.X
                maxY <- max.Y
                first <- true
        )
    )

    let mapBounds = 
        {
            Min = Vector2 (minX, minY)
            Max = Vector2 (maxX, maxY)
        }

    //spawnBounds mapBounds

    let physicsWorld = Physics.init ()
    let capsule = Physics.addCapsuleController defaultPosition (24.f) (20.f) physicsWorld

    sectorPolygons
    |> Seq.iter (fun (flats, sector) ->
        flats
        |> Seq.iter (fun flat ->
            let vertices =
                flat.Triangles
                |> Array.map (fun x -> [|x.X;x.Y;x.Z|])
                |> Array.reduce Array.append
                |> Array.map (fun x -> Vector3 (x.X, x.Y, single sector.FloorHeight))

            Physics.addTriangles vertices vertices.Length physicsWorld
        )
    )

    //sectorPolygons
    //|> Seq.iter (fun (flats, sector) ->
    //    flats
    //    |> Seq.iter (fun flat ->
    //        let vertices =
    //            flat.Triangles
    //            |> Array.map (fun x -> [|x.Z;x.Y;x.X|])
    //            |> Array.reduce Array.append
    //            |> Array.map (fun x -> Vector3 (x.X, x.Y, single sector.CeilingHeight))

    //        Physics.addTriangles vertices vertices.Length physicsWorld
    //    )
    //)

    //lvl.Sectors
    //|> Seq.iter (fun sector ->
    //    lvl
    //    |> Level.createWalls sector
    //    |> Seq.iter (fun wall ->
    //        Physics.addTriangles wall.Vertices wall.Vertices.Length physicsWorld
    //    )
    //)

    let mutable xpos = 0
    let mutable prevXpos = 0

    let mutable ypos = 0
    let mutable prevYpos = 0

    let mutable isMovingForward = false
    let mutable isMovingLeft = false
    let mutable isMovingRight = false
    let mutable isMovingBackward = false

    let sectorChecks =
        EntitySystem.create "SectorChecks" 
            [
                update (fun entityManager eventManager deltaTime ->

                    Input.pollEvents (app.Window)
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
                                let v = Vector3.Transform (Vector3.UnitZ * -64.f, transformComp.Rotation)
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                transformComp.Translate (v)

                            if isMovingLeft then
                                let v = Vector3.Transform (Vector3.UnitX * -64.f, transformComp.Rotation)
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                transformComp.Translate (v)

                            if isMovingBackward then
                                let v = Vector3.Transform (Vector3.UnitZ * 64.f, transformComp.Rotation)
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                transformComp.Translate (v)

                            if isMovingRight then
                                let v = Vector3.Transform (Vector3.UnitX * 64.f, transformComp.Rotation)
                                //Physics.applyForce (Vector3 (v.X, v.Y, 0.f)) (transformComp.Position) capsule
                                transformComp.Translate (v)
                               
                        )
                    )

                    //Physics.preStepKinematicController capsule physicsWorld
                    Physics.stepKinematicController deltaTime capsule physicsWorld
                    //Physics.step deltaTime physicsWorld
                    //Physics.stepKinematicController deltaTime capsule physicsWorld

                    //let position = Physics.getKinematicControllerPosition capsule

                    //match entityManager.TryFind<CameraComponent, TransformComponent> (fun _ _ _ -> true) with
                    //| Some (ent, _, transformComp) ->
                    //    transformComp.Position <- position + Vector3 (0.f, 0.f, 56.f / 2.f)
                    //| _ -> ()
                )
            ]

    { 
        Window = app.Window
        Update = world.AddSystem sectorChecks
        RenderUpdate = updateSys1
        Level = lvl
    }

let draw t (prev: ClientState) (curr: ClientState) =

    let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

    curr.RenderUpdate t

    stopwatch.Stop ()

    //printfn "%A" stopwatch.Elapsed.TotalMilliseconds