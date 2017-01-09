﻿[<RequireQualifiedAccess>] 
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
open Foom.Common.Components
open Foom.Wad.Components

let exportFlatTextures (wad: Wad) =
    wad
    |> Wad.iterFlatTextureName (fun name ->
        Wad.tryFindFlatTexture name wad
        |> Option.iter (fun tex ->
            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iter (fun p ->
                if p.Equals Pixel.Cyan then
                    isTransparent <- true
            )

            let format =
                if isTransparent then
                    Imaging.PixelFormat.Format32bppArgb
                else
                    Imaging.PixelFormat.Format24bppRgb

            let bmp = new Bitmap(width, height, format)

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

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iter (fun p ->
                if p.Equals Pixel.Cyan then
                    isTransparent <- true
            )

            let format =
                if isTransparent then
                    Imaging.PixelFormat.Format32bppArgb
                else
                    Imaging.PixelFormat.Format24bppRgb

            let bmp = new Bitmap(width, height, format)

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

let exportSpriteTextures (wad: Wad) =
    wad
    |> Wad.iterSpriteTextureName (fun name ->
        Wad.tryFindSpriteTexture name wad
        |> Option.iter (fun tex ->
            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iter (fun p ->
                if p.Equals Pixel.Cyan then
                    isTransparent <- true
            )

            let format =
                if isTransparent then
                    Imaging.PixelFormat.Format32bppArgb
                else
                    Imaging.PixelFormat.Format24bppRgb

            let bmp = new Bitmap(width, height, format)

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

let globalBatch = Dictionary<string, Vector3 ResizeArray * Vector2 ResizeArray * Color ResizeArray> ()

let runGlobalBatch (em: EntityManager) =
    globalBatch
    |> Seq.iter (fun pair ->
        let texturePath = pair.Key
        let vertices, uv, color = pair.Value

        let ent = em.Spawn ()

        em.Add (ent, TransformComponent (Matrix4x4.Identity))

        let meshInfo : RendererSystem.MeshInfo =
            {
                Position = vertices |> Seq.toArray
                Uv = uv |> Seq.toArray
                Color = color |> Seq.toArray
            }

        let materialInfo : RendererSystem.MaterialInfo =
            {
                ShaderInfo =
                    {
                        VertexShader = "triangle.vertex"
                        FragmentShader = "triangle.fragment"
                    }
            
                TextureInfo =
                    {
                        TexturePath = texturePath
                    }
            }

        let renderInfo : RendererSystem.RenderInfo =
            {
                MeshInfo = meshInfo
                MaterialInfo = materialInfo
            }

        em.Add (ent, RendererSystem.RenderInfoComponent (renderInfo))
    )

open System.Linq

let spawnMesh (vertices: IEnumerable<Vector3>) uv texturePath lightLevel (em: EntityManager) =
    let color = Array.init (vertices.Count ()) (fun _ -> Color.FromArgb(255, int lightLevel, int lightLevel, int lightLevel))

    match globalBatch.TryGetValue(texturePath) with
    | true, (gVertices, gUv, gColor) ->
        gVertices.AddRange(vertices)
        gUv.AddRange(uv)
        gColor.AddRange(color)
    | _ ->
        globalBatch.Add (texturePath, (ResizeArray vertices, ResizeArray uv, ResizeArray color))

let spawnCeilingMesh (flat: Flat) lightLevel wad em =
    flat.Ceiling.TextureName
    |> Option.iter (fun textureName ->
        let texturePath = textureName + "_flat.bmp"
        let t = new Bitmap(texturePath)
        spawnMesh flat.Ceiling.Vertices (FlatPart.createUV t.Width t.Height flat.Ceiling) texturePath lightLevel em
    )

let spawnFloorMesh (flat: Flat) lightLevel wad em =
    flat.Floor.TextureName
    |> Option.iter (fun textureName ->
        let texturePath = textureName + "_flat.bmp"
        let t = new Bitmap(texturePath)
        spawnMesh flat.Floor.Vertices (FlatPart.createUV t.Width t.Height flat.Floor) texturePath lightLevel em
    )

let spawnWallPartMesh (part: WallPart) lightLevel wad em isSky =
    if not isSky then
        part.TextureName
        |> Option.iter (fun textureName ->
            let texturePath = textureName + ".bmp"
            let t = new Bitmap(texturePath)
            spawnMesh part.Vertices (WallPart.createUV t.Width t.Height part) texturePath lightLevel em
        )
    else
        let texturePath = "F_SKY1" + "_flat.bmp"
        let t = new Bitmap(texturePath)
        spawnMesh part.Vertices (WallPart.createUV t.Width t.Height part) texturePath lightLevel em

let spawnWallSideMesh (part: WallPart option) lightLevel wad em isSky =
    part
    |> Option.iter (fun part ->
        spawnWallPartMesh part lightLevel wad em isSky
    )

let spawnWallMesh (wall: Wall) level wad em =
    match wall.FrontSide with
    | Some frontSide ->

        let isSky =
            match wall.BackSide with
            | Some backSide ->
                let sector = Level.getSector backSide.SectorId level
                sector.CeilingTextureName.Equals("F_SKY1")
            | _ -> false
        
        let lightLevel = Level.lightLevelBySectorId frontSide.SectorId level
        spawnWallSideMesh frontSide.Upper lightLevel wad em isSky
        spawnWallSideMesh frontSide.Middle lightLevel wad em false
        spawnWallSideMesh frontSide.Lower lightLevel wad em false

    | _ -> ()

    match wall.BackSide with
    | Some backSide ->

        let isSky =
            match wall.FrontSide with
            | Some frontSide ->
                let sector = Level.getSector frontSide.SectorId level
                sector.CeilingTextureName.Equals("F_SKY1")
            | _ -> false

        let lightLevel = Level.lightLevelBySectorId backSide.SectorId level
        spawnWallSideMesh backSide.Upper lightLevel wad em isSky
        spawnWallSideMesh backSide.Middle lightLevel wad em false
        spawnWallSideMesh backSide.Lower lightLevel wad em false

    | _ -> ()

let updates (clientWorld: ClientWorld) =
    [
        Behavior.wadLoading
            (fun name -> System.IO.File.Open (name, FileMode.Open) :> Stream)
            (fun wad _ ->
                wad |> exportFlatTextures
                wad |> exportTextures
                wad |> exportSpriteTextures
            )

        Behavior.levelLoading (fun wad level em ->
            let physicsEngineComp = PhysicsEngineComponent.Create 128

            level
            |> Level.iteriSector (fun i sector ->

                let lightLevel = sector.LightLevel

                (i, level)
                ||> Level.iterLinedefBySectorId (fun linedef ->
                    let wut = not (linedef.Flags.HasFlag(LinedefFlags.UpperTextureUnpegged))
                    let staticWall =
                        {
                            LineSegment = (LineSegment2D (linedef.Start, linedef.End))

                            IsTrigger = (linedef.FrontSidedef.IsNone || linedef.BackSidedef.IsNone) |> not

                        }

                    let rBody = RigidBody (StaticWall staticWall, Vector3.Zero)

                    physicsEngineComp.PhysicsEngine
                    |> PhysicsEngine.addRigidBody rBody
                )

                WadLevel.createFlats i level
                |> Seq.iter (fun flat ->
                    let lightLevel = Level.lightLevelBySectorId i level
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
            )

            WadLevel.createWalls level
            |> Seq.iter (fun wall ->
                spawnWallMesh wall level wad em
            )

            level
            |> Level.iterThing (fun thing ->
                match thing with
                | Thing.Doom thing ->
                    if thing.Type = ThingType.Barrel then
                        let bitmap = new Bitmap("BAR1A0.bmp")
                        let halfWidth = single bitmap.Width / 2.f

                        
                        let pos = Vector2 (single thing.X, single thing.Y)
                        let sector = physicsEngineComp.PhysicsEngine |> PhysicsEngine.findWithPoint pos :?> Sector
                        let pos = Vector3 (pos, single sector.FloorHeight)
                        let vertices =
                            [|
                                Vector3 (-halfWidth, 0.f, 0.f) + pos
                                Vector3 (halfWidth, 0.f, 0.f) + pos
                                Vector3 (halfWidth, 0.f, single bitmap.Height) + pos
                                Vector3 (halfWidth, 0.f, single bitmap.Height) + pos
                                Vector3 (-halfWidth, 0.f, single bitmap.Height) + pos
                                Vector3 (-halfWidth, 0.f, 0.f) + pos
                            |]

                        let uv =
                            [|
                                Vector2 (0.f, 0.f * -1.f)
                                Vector2 (1.f, 0.f * -1.f)
                                Vector2 (1.f, 1.f * -1.f)
                                Vector2 (1.f, 1.f * -1.f)
                                Vector2 (0.f, 1.f * -1.f)
                                Vector2 (0.f, 0.f * -1.f)
                            |]

                        let lightLevel = Level.lightLevelBySectorId sector.Id level
                        spawnMesh vertices uv "BAR1A0.bmp" lightLevel em
                        ()
                | _ -> ()
            )

            runGlobalBatch em
            em.Add (clientWorld.Entity, physicsEngineComp)

            level
            |> Level.tryFindPlayer1Start
            |> Option.iter (function
                | Doom doomThing ->
                    let sector =
                        physicsEngineComp.PhysicsEngine
                        |> PhysicsEngine.findWithPoint (Vector2 (single doomThing.X, single doomThing.Y)) :?> Sector

                    let position = Vector3 (single doomThing.X, single doomThing.Y, single sector.FloorHeight + 28.f)

                    let cameraEnt = em.Spawn ()
                    em.Add (cameraEnt, CameraComponent (Matrix4x4.CreatePerspectiveFieldOfView (56.25f * 0.0174533f, ((16.f + 16.f * 0.25f) / 9.f), 16.f, 100000.f)))
                    em.Add (cameraEnt, TransformComponent (Matrix4x4.CreateTranslation (position)))
                    em.Add (cameraEnt, CharacterControllerComponent (position, 17.1f, 56.f))
                    em.Add (cameraEnt, PlayerComponent ())

                | _ -> ()
            )
        )
    ]
