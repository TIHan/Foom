namespace Foom.Level.Components

open System
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Geometry
open Foom.Wad.Level

type DoomLevelTexture =
    {
        TextureName: string
        CreateUV: int -> int -> Vector2 []
    }

type DoomLevelStaticGeometry =
    {
        Texture: DoomLevelTexture option
        LightLevel: byte
        Vertices: Vector3 []
    }

type DoomLevelComponent (level: Level) =

    let calculateStaticGeometry =
        lazy
            level.Sectors
            |> Seq.mapi (fun i sector ->
                let flats = Level.createFlats i level
                let walls = Level.createWalls sector level
                let lightLevel = sector.LightLevel
                let lightLevel = lightLevel * lightLevel / 255
                let lightLevel =
                    if lightLevel > 255 then 255uy
                    else byte lightLevel

                let floorGeometry =
                    flats
                    |> Seq.map (fun flat ->
                        let texture =
                            match flat.FloorTextureName with
                            | Some name ->
                                {
                                    TextureName = name
                                    CreateUV = fun width height -> Flat.createUV width height flat 
                                } |> Some
                            | _ -> None
                        {
                            Texture = texture
                            LightLevel = lightLevel
                            Vertices = 
                                flat.Triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.X.X, tri.X.Y, single flat.FloorHeight)
                                        Vector3 (tri.Y.X, tri.Y.Y, single flat.FloorHeight)
                                        Vector3 (tri.Z.X, tri.Z.Y, single flat.FloorHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append
                        }
                    )

                let ceilingGeometry =
                    flats
                    |> Seq.map (fun flat ->
                        let texture =
                            match flat.CeilingTextureName with
                            | Some name ->
                                {
                                    TextureName = name
                                    CreateUV = fun width height -> Flat.createUV width height flat 
                                } |> Some
                            | _ -> None
                        {
                            Texture = texture
                            LightLevel = lightLevel
                            Vertices = 
                                flat.Triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.Z.X, tri.Z.Y, single flat.CeilingHeight)
                                        Vector3 (tri.Y.X, tri.Y.Y, single flat.CeilingHeight)
                                        Vector3 (tri.X.X, tri.X.Y, single flat.CeilingHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append
                        }
                    )

                let wallGeometry =
                    walls
                    |> Seq.map (fun wall ->
                        let texture =
                            match wall.TextureName with
                            | Some name ->
                                {
                                    TextureName = name
                                    CreateUV = fun width height -> Wall.createUV width height wall
                                } |> Some
                            | _ -> None
                        {
                            Texture = texture
                            LightLevel = lightLevel
                            Vertices = wall.Vertices
                        }
                    )

                [|
                    floorGeometry
                    ceilingGeometry
                    wallGeometry
                |]
                |> Array.reduce Seq.append
            )
            |> Seq.reduce Seq.append

    member this.StaticGeometry = calculateStaticGeometry.Force()

    interface IEntityComponent