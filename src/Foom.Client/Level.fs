[<RequireQualifiedAccess>] 
module Foom.Client.Level

open System
open System.IO
open System.Drawing
open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Physics
open Foom.Renderer
open Foom.Geometry
open Foom.Wad
open Foom.Wad.Level
open Foom.Wad.Level.Structures
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

let tryFindFlatTexture name wad =
    match name with
    | Some name ->
        match Wad.tryFindFlatTexture name wad with
        | Some tex -> Some tex
        | _ -> None
    | _ -> None

let tryFindTexture name wad =
    match name with
    | Some name ->
        match Wad.tryFindTexture name wad with
        | Some tex -> Some tex
        | _ -> None
    | _ -> None

let spawnMesh vertices uv texturePath lightLevel (em: EntityManager) =
    let ent = em.Spawn ()

    em.AddComponent ent (TransformComponent (Matrix4x4.Identity))
    em.AddComponent ent (MeshComponent (vertices, uv))
    em.AddComponent ent (
        MaterialComponent (
            "triangle.vertex",
            "triangle.fragment",
            texturePath,
            { R = lightLevel; G = lightLevel; B = lightLevel; A = 0uy }
        )
    )

let spawnCeilingMesh (flat: Flat) lightLevel wad em =
    tryFindFlatTexture flat.Ceiling.TextureName wad
    |> Option.iter (fun tex ->
        let width = Array2D.length1 tex.Data
        let height = Array2D.length2 tex.Data
        let texturePath = tex.Name + "_flat.bmp"

        spawnMesh flat.Ceiling.Vertices (Flat.createCeilingUV width height flat) texturePath lightLevel em
    )

let spawnFloorMesh (flat: Flat) lightLevel wad em =
    tryFindFlatTexture flat.Floor.TextureName wad
    |> Option.iter (fun tex ->
        let width = Array2D.length1 tex.Data
        let height = Array2D.length2 tex.Data
        let texturePath = tex.Name + "_flat.bmp"

        spawnMesh flat.Floor.Vertices (Flat.createFloorUV width height flat) texturePath lightLevel em
    )

let spawnWallMesh (wall: Wall) lightLevel wad em =
    tryFindTexture wall.TextureName wad
    |> Option.iter (fun tex ->
        let width = Array2D.length1 tex.Data
        let height = Array2D.length2 tex.Data
        let texturePath = tex.Name + ".bmp"

        spawnMesh wall.Vertices (Wall.createUV width height wall) texturePath lightLevel em
    )

let spawnAABBWireframe (aabb: AABB2D) (em: EntityManager) =
    let min = aabb.Min ()
    let max = aabb.Max ()

    let ent = em.Spawn ()
    em.AddComponent ent (TransformComponent (Matrix4x4.Identity))
    em.AddComponent ent (
        WireframeComponent (
            [|
                Vector3 (min.X, min.Y, 0.f)
                Vector3 (max.X, min.Y, 0.f)

                Vector3 (max.X, min.Y, 0.f)
                Vector3 (max.X, max.Y, 0.f)

                Vector3 (max.X, max.Y, 0.f)
                Vector3 (min.X, max.Y, 0.f)

                Vector3 (min.X, max.Y, 0.f)
                Vector3 (min.X, min.Y, 0.f)
            |]
        )
    ) 
    em.AddComponent ent (
        MaterialComponent (
            "v.vertex",
            "f.fragment",
            "",
            { R = 255uy; G = 255uy; B = 255uy; A = 0uy }
        )
    )

let updates () =
    [
        Behavior.wadLoading
            (fun name -> System.IO.File.Open (name, FileMode.Open) :> Stream)
            (fun wad _ ->
                wad |> exportFlatTextures
                wad |> exportTextures
            )

        Behavior.levelLoading (fun wad level em ->
            let levelAABB = Level.getAABB level

            spawnAABBWireframe levelAABB em

            let physicsEngineComp = PhysicsEngineComponent.Create 128 

            level
            |> Level.iteriSector (fun i sector ->
                let lightLevel = Level.lightLevelBySectorId sector.Id level

                Level.createFlats i level
                |> Seq.iter (fun flat ->
                    spawnCeilingMesh flat lightLevel wad em
                    spawnFloorMesh flat lightLevel wad em

                    let mutable j = 0
                    while j < flat.Floor.Vertices.Length do
                        let v0 = flat.Floor.Vertices.[j]
                        let v1 = flat.Floor.Vertices.[j + 1]
                        let v2 = flat.Floor.Vertices.[j + 2]

                        physicsEngineComp.PhysicsEngine
                        |> PhysicsEngine.addTriangle
                            (Triangle2D (
                                    Vector2 (v0.X, v0.Y),
                                    Vector2 (v1.X, v1.Y),
                                    Vector2 (v2.X, v2.Y)
                                )
                            )
                            i

                        j <- j + 3
                )

                Level.createWalls i level
                |> Seq.iter (fun wall ->
                    spawnWallMesh wall lightLevel wad em
                )
            )

            let ent = em.Spawn ()
            em.AddComponent ent (physicsEngineComp)

            level
            |> Level.tryFindPlayer1Start
            |> Option.iter (function
                | Doom doomThing ->
                    match (level |> Level.sectorAt (Vector2 (single doomThing.X, single doomThing.Y))) with
                    | Some sector ->

                        let position = Vector3 (single doomThing.X, single doomThing.Y, single sector.FloorHeight + 28.f)

                        let cameraEnt = em.Spawn ()
                        em.AddComponent cameraEnt (CameraComponent (Matrix4x4.CreatePerspectiveFieldOfView (56.25f * 0.0174533f, ((16.f + 16.f * 0.25f) / 9.f), 16.f, System.Single.MaxValue)))
                        em.AddComponent cameraEnt (TransformComponent (Matrix4x4.CreateTranslation (position)))

                    | _ -> ()
                | _ -> ()
            )
        )
    ]
