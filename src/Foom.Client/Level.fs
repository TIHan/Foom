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

let spawnFrontWallPartSideMesh (wallPart: WallPart) (side: WallPartSide) lightLevel wad em =
    side.TextureName
    |> Option.iter (fun textureName ->
        let texturePath = textureName + ".bmp"
        let t = new Bitmap(texturePath)
        spawnMesh side.Vertices (WallPart.createFrontUV t.Width t.Height wallPart) texturePath lightLevel em
    )

let spawnBackWallPartSideMesh (wallPart: WallPart) (side: WallPartSide) lightLevel wad em =
    side.TextureName
    |> Option.iter (fun textureName ->
        let texturePath = textureName + ".bmp"
        let t = new Bitmap(texturePath)
        spawnMesh side.Vertices (WallPart.createBackUV t.Width t.Height wallPart) texturePath lightLevel em
    )

let spawnWallPartMesh (wallPart: WallPart) lightLevel wad em =
    wallPart.FrontSide
    |> Option.iter (fun side ->
        spawnFrontWallPartSideMesh wallPart side lightLevel wad em
    )

    wallPart.BackSide
    |> Option.iter (fun side ->
        spawnBackWallPartSideMesh wallPart side lightLevel wad em
    )

let spawnWallMesh (wall: Wall) lightLevel wad em =
    spawnWallPartMesh wall.Upper lightLevel wad em
    spawnWallPartMesh wall.Middle lightLevel wad em
    spawnWallPartMesh wall.Lower lightLevel wad em

let updates (clientWorld: ClientWorld) =
    [
        Behavior.wadLoading
            (fun name -> System.IO.File.Open (name, FileMode.Open) :> Stream)
            (fun wad _ ->
                ()
                wad |> exportFlatTextures
                wad |> exportTextures
            )

        Behavior.levelLoading (fun wad level em ->
            let physicsEngineComp = PhysicsEngineComponent.Create 128

            level
            |> Level.iteriSector (fun i sector ->
//                if i <> 279 then ()
//                else

                let lightLevel = Level.lightLevelBySectorId sector.Id level
                ()

                sector.Linedefs
                |> List.iter (fun linedef ->
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

                Flat.createFlats i level
                |> Seq.iter (fun flat ->
                    ()
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
                    ()
                    spawnWallMesh wall lightLevel wad em
                )
            )

            runGlobalBatch em
            em.Add (clientWorld.Entity, physicsEngineComp)

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
