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
open Foom.Level
open Foom.Wad
open Foom.Wad.Level
open Foom.Wad.Level.Structures
open Foom.Common.Components
open Foom.Wad.Components

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

let spawnMesh vertices uv texturePath lightLevel (em: EntityManager) =
    let ent = em.Spawn ()

    em.Add (ent, TransformComponent (Matrix4x4.Identity))
    em.Add (ent,
        {
            Position = Vector3ArrayBuffer (vertices)
            Uv = Vector2ArrayBuffer (uv)
        }
    )
    em.Add (ent,
        MaterialComponent (
            "triangle.vertex",
            "triangle.fragment",
            texturePath,
            Color.FromArgb (0, int lightLevel, int lightLevel, int lightLevel)
        )
    )

let textureCache = Dictionary<string, Texture2DBuffer> ()

let spawnCeilingMesh (flat: Flat) lightLevel wad em =
    flat.Ceiling.TextureName
    |> Option.iter (fun textureName ->
        let texture : Texture2DBuffer option =
            match textureCache.TryGetValue (textureName + "_flat") with
            | true, tex -> Some tex
            | _ ->
                try
                    let bmp = new Bitmap (textureName + "_flat.bmp")
                    let t = Texture2DBuffer (bmp)
                    textureCache.[textureName + "_flat"] <- t
                    Some t
                with | _ -> None


        match texture with
        | Some t ->
            spawnMesh flat.Ceiling.Vertices (FlatPart.createUV t.Width t.Height flat.Ceiling) texture lightLevel em
        | _ -> ()
    )

let spawnFloorMesh (flat: Flat) lightLevel wad em =
    flat.Floor.TextureName
    |> Option.iter (fun textureName ->
        let texture =
            match textureCache.TryGetValue (textureName + "_flat") with
            | true, tex -> Some tex
            | _ ->
                try
                    let bmp = new Bitmap (textureName + "_flat.bmp")
                    let t = Texture2DBuffer (bmp)
                    textureCache.[textureName + "_flat"] <- t
                    Some t
                with | _ -> None


        match texture with
        | Some t ->
            spawnMesh flat.Floor.Vertices (FlatPart.createUV t.Width t.Height flat.Floor) texture lightLevel em
        | _ -> ()
    )

let spawnWallPartMesh (wallPart: WallPart) lightLevel wad em =
    wallPart.TextureName
    |> Option.iter (fun textureName ->
        let texture =
            match textureCache.TryGetValue (textureName) with
            | true, tex -> Some tex
            | _ ->
                try
                    let bmp = new Bitmap (textureName + ".bmp")
                    let t = Texture2DBuffer (bmp)
                    textureCache.[textureName] <- t
                    Some t
                with | _ -> None


        match texture with
        | Some t ->
            spawnMesh wallPart.Vertices (WallPart.createUV t.Width t.Height wallPart) texture lightLevel em
        | _ -> ()
    )

let spawnWallMesh (wall: Wall) lightLevel wad em =
    wall.Upper |> Option.iter (fun upper -> spawnWallPartMesh upper lightLevel wad em)
    wall.Middle |> Option.iter (fun middle -> spawnWallPartMesh middle lightLevel wad em)
    wall.Lower |> Option.iter (fun lower -> spawnWallPartMesh lower lightLevel wad em)

let spawnAABBWireframe (aabb: AABB2D) (em: EntityManager) =
    let min = aabb.Min ()
    let max = aabb.Max ()

    let ent = em.Spawn ()
    em.Add (ent, TransformComponent (Matrix4x4.Identity))
    em.Add (ent,
        {
            WireframeComponent.Position =
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
                |> Vector3ArrayBuffer
        }
    )
    em.Add (ent,
        MaterialComponent (
            "v.vertex",
            "f.fragment",
            None,
            Color.FromArgb (0, 255, 255, 255)
        )
    )

let updates (clientWorld: ClientWorld) =
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

                sector.Linedefs
                |> List.iter (fun linedef ->
                    let wut = not (linedef.Flags.HasFlag(LinedefFlags.UpperTextureUnpegged))
                    let staticWall =
                        {
                            LineSegment = (LineSegment2D (linedef.Start, linedef.End))

                            IsTrigger = (linedef.FrontSidedef.IsNone || linedef.BackSidedef.IsNone || wut) |> not

                        }

                    let rBody = RigidBody (StaticWall staticWall, Vector3.Zero)

                    physicsEngineComp.PhysicsEngine
                    |> PhysicsEngine.addRigidBody rBody
                )

                Flat.createFlats i level
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
                            sector // data to store for physics

                        j <- j + 3
                )

                Wall.createWalls i level
                |> Seq.iter (fun wall ->
                    spawnWallMesh wall lightLevel wad em
                )
            )

            em.Add (clientWorld.Entity, physicsEngineComp)

            // *** TEMPORARY ***
            em.Add (clientWorld.Entity, TransformComponent (Matrix4x4.Identity))
            em.Add (clientWorld.Entity,
                {
                    WireframeComponent.Position =
                        [||]
                        |> Vector3ArrayBuffer
                }
            )
            em.Add (clientWorld.Entity,
                MaterialComponent (
                    "v.vertex",
                    "f.fragment",
                    None,
                    Color.FromArgb (0, 255, 255, 255)
                )
            )
            // *****************

            level
            |> Level.tryFindPlayer1Start
            |> Option.iter (function
                | Doom doomThing ->
                    match (level |> Level.sectorAt (Vector2 (single doomThing.X, single doomThing.Y))) with
                    | Some sector ->

                        let position = Vector3 (single doomThing.X, single doomThing.Y, single sector.FloorHeight + 28.f)

                        let cameraEnt = em.Spawn ()
                        em.Add (cameraEnt, CameraComponent (Matrix4x4.CreatePerspectiveFieldOfView (56.25f * 0.0174533f, ((16.f + 16.f * 0.25f) / 9.f), 16.f, 100000.f)))
                        em.Add (cameraEnt, TransformComponent (Matrix4x4.CreateTranslation (position)))
                        em.Add (cameraEnt, CharacterControllerComponent (position, 17.1f, 56.f))

                    | _ -> ()
                | _ -> ()
            )
        )
    ]
