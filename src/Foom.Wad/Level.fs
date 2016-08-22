namespace Foom.Wad.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type WallSpecial =
    | Nothing
    | Door of ceilingSectorId: int

type Wall =
    {
        SectorId: int
        TextureName: string option
        TextureOffsetX: int
        TextureOffsetY: int
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
        Special: WallSpecial
    }

type Ceiling =
    {
        Vertices: Vector3 []
        Height: int
        TextureName: string option
    }

type Floor =
    {
        Vertices: Vector3 []
        Height: int
        TextureName: string option
    }

type Flat =
    {
        SectorId: int
        Ceiling: Ceiling
        Floor: Floor
    }

type Level =
    {
        sectors: Sector []
    }

    member this.Sectors = this.sectors |> Seq.ofArray

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Flat =

    let createFloorUV width height (flat: Flat) =
        let vertices = flat.Floor.Vertices
        let width = single width
        let height = single height * -1.f
        let uv = Array.zeroCreate (vertices.Length * sizeof<Vector3>)

        let mutable i = 0
        while i < vertices.Length do

            let v1 = vertices.[i]
            let v2 = vertices.[i + 1]
            let v3 = vertices.[i + 2]

            uv.[i] <- Vector2 (v1.X / width, v1.Y / height)
            uv.[i + 1] <- Vector2 (v2.X / width, v2.Y / height)
            uv.[i + 2] <- Vector2 (v3.X / width, v3.Y / height)

            i <- i + 3

        uv

    let createCeilingUV width height (flat: Flat) =
        let vertices = flat.Ceiling.Vertices
        let width = single width
        let height = single height * -1.f
        let uv = Array.zeroCreate (vertices.Length * sizeof<Vector3>)

        let mutable i = 0
        while i < vertices.Length do

            let v1 = vertices.[i]
            let v2 = vertices.[i + 1]
            let v3 = vertices.[i + 2]

            uv.[i] <- Vector2 (v1.X / width, v1.Y / height)
            uv.[i + 1] <- Vector2 (v2.X / width, v2.Y / height)
            uv.[i + 2] <- Vector2 (v3.X / width, v3.Y / height)

            i <- i + 3

        uv

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wall =

    let createUV width height (wall: Wall) =
        let vertices = wall.Vertices
        let uv = Array.zeroCreate vertices.Length

        let mutable i = 0
        while (i < vertices.Length) do
            let p1 = vertices.[i]
            let p2 = vertices.[i + 1]
            let p3 = vertices.[i + 2]

            let width = single width
            let height = single height

            let v1 = Vector2 (p1.X, p1.Y)
            let v2 = Vector2 (p2.X, p2.Y)
            let v3 = Vector2 (p3.X, p3.Y)

            let one = 0.f + single wall.TextureOffsetX
            let two = (v2 - v1).Length ()

            let x, y, z1, z3 =

                // lower unpeg
                match wall.TextureAlignment with
                | LowerUnpegged ->
                    let ofsY = single wall.TextureOffsetY / height * -1.f
                    if p3.Z < p1.Z then
                        (one + two) / width, 
                        one / width, 
                        0.f - ofsY,
                        ((abs (p1.Z - p3.Z)) / height * -1.f) - ofsY
                    else
                        one / width, 
                        (one + two) / width, 
                        ((abs (p1.Z - p3.Z)) / height * -1.f) - ofsY,
                        0.f - ofsY

                // upper unpeg
                | UpperUnpegged offsetY ->
                    let z = single offsetY / height * -1.f
                    let ofsY = single wall.TextureOffsetY / height * -1.f
                    if p3.Z < p1.Z then
                        (one + two) / width, 
                        one / width, 
                        (1.f - ((abs (p1.Z - p3.Z)) / height * -1.f)) - z - ofsY,
                        1.f - z - ofsY
                    else
                        one / width, 
                        (one + two) / width, 
                        1.f - z - ofsY,
                        (1.f - ((abs (p1.Z - p3.Z)) / height * -1.f)) - z - ofsY

            

            uv.[i] <- Vector2 (x, z3)
            uv.[i + 1] <- Vector2(y, z3)
            uv.[i + 2] <- Vector2(y, z1)

            i <- i + 3
        uv

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    let lightLevelBySectorId sectorId (level: Level) =
        let sector = level.sectors.[sectorId]
        let lightLevel = sector.LightLevel
        let lightLevel = lightLevel * lightLevel / 255
        if lightLevel > 255 then 255uy
        else byte lightLevel

    let createFlats sectorId level = 
        match level.sectors |> Array.tryItem sectorId with
        | None -> Seq.empty
        | Some sector ->

            match LinedefTracer.run2 (sector.Linedefs) sectorId with
            | [] -> Seq.empty
            | linedefPolygons ->
                let rec map (linedefPolygons: LinedefPolygon list) =
                    linedefPolygons
                    |> List.map (fun x -> 
                        {
                            Polygon = (x.Linedefs, sectorId) ||> Polygon.ofLinedefs
                            Children = map x.Inner
                        }
                    )

                let sectorTriangles = 
                    map linedefPolygons
                    |> Seq.map (Foom.Wad.Geometry.Triangulation.EarClipping.computeTree)
                    |> Seq.reduce Seq.append

                sectorTriangles
                |> Seq.map (fun triangles ->
                  
                    let ceiling =
                        {
                            Ceiling.Vertices =
                                triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.Z.X, tri.Z.Y, single sector.CeilingHeight)
                                        Vector3 (tri.Y.X, tri.Y.Y, single sector.CeilingHeight)
                                        Vector3 (tri.X.X, tri.X.Y, single sector.CeilingHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append

                            Height = sector.CeilingHeight
                            TextureName = Some sector.CeilingTextureName
                        }

                    let floor =
                        {
                            Floor.Vertices =
                                triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.X.X, tri.X.Y, single sector.FloorHeight)
                                        Vector3 (tri.Y.X, tri.Y.Y, single sector.FloorHeight)
                                        Vector3 (tri.Z.X, tri.Z.Y, single sector.FloorHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append
                               
                            Height = sector.FloorHeight
                            TextureName = Some sector.FloorTextureName
                        }

                    {
                        SectorId = sectorId
                        Ceiling = ceiling
                        Floor = floor
                    }
                )

    let createWalls (sectorId: int) level =
        let arr = ResizeArray<Wall> ()
        let sector = level.sectors.[sectorId]

        sector.Linedefs
        |> Seq.iter (fun linedef ->
            let isLowerUnpegged = linedef.Flags.HasFlag(LinedefFlags.LowerTextureUnpegged)
            let isUpperUnpegged = linedef.Flags.HasFlag(LinedefFlags.UpperTextureUnpegged)
            let isTwoSided = linedef.Flags.HasFlag(LinedefFlags.TwoSided)

            let special =
                if linedef.SpecialType = 1 && linedef.BackSidedef.IsSome then
                    Door (linedef.BackSidedef.Value.SectorNumber)
                else
                    Nothing

            let addMiddleWithVertices (floorHeight: int) (ceilingHeight: int) (sidedef: Sidedef) vertices =
                {
                    SectorId = sidedef.SectorNumber
                    TextureName = Some sidedef.MiddleTextureName
                    TextureOffsetX = sidedef.OffsetX
                    TextureOffsetY = sidedef.OffsetY
                    Vertices = vertices
                    TextureAlignment =
                        if isLowerUnpegged then
                            LowerUnpegged
                        else
                            UpperUnpegged 0
                    Special = special
                } |> arr.Add

            let addMiddleFront floorHeight ceilingHeight sidedef =
                addMiddleWithVertices floorHeight ceilingHeight sidedef
                    [|
                        Vector3 (linedef.Start, single floorHeight)
                        Vector3 (linedef.End, single floorHeight)
                        Vector3 (linedef.End, single ceilingHeight)

                        Vector3 (linedef.End, single ceilingHeight)
                        Vector3 (linedef.Start, single ceilingHeight)
                        Vector3 (linedef.Start, single floorHeight)
                    |]

            let addMiddleBack floorHeight ceilingHeight sidedef =
                addMiddleWithVertices floorHeight ceilingHeight sidedef <|
                    [|
                        Vector3 (linedef.End, single floorHeight)
                        Vector3 (linedef.Start, single floorHeight)
                        Vector3 (linedef.Start, single ceilingHeight)

                        Vector3 (linedef.Start, single ceilingHeight)
                        Vector3 (linedef.End, single ceilingHeight)
                        Vector3 (linedef.End, single floorHeight)
                    |]

            match linedef.BackSidedef with
            | Some backSidedef when backSidedef.SectorNumber = sectorId ->

                match linedef.FrontSidedef with
                | Some frontSidedef ->
                    let frontSideSector = Seq.item frontSidedef.SectorNumber level.sectors

                    if backSidedef.UpperTextureName.Contains("-") |> not && frontSideSector.CeilingHeight < sector.CeilingHeight then

                        {
                            SectorId = backSidedef.SectorNumber
                            TextureName = Some backSidedef.UpperTextureName
                            TextureOffsetX = backSidedef.OffsetX
                            TextureOffsetY = backSidedef.OffsetY
                            Vertices =
                                [|
                                    Vector3 (linedef.End, single frontSideSector.CeilingHeight)
                                    Vector3 (linedef.Start, single frontSideSector.CeilingHeight)
                                    Vector3 (linedef.Start, single sector.ceilingHeight)

                                    Vector3 (linedef.Start, single sector.CeilingHeight)
                                    Vector3 (linedef.End, single sector.CeilingHeight)
                                    Vector3 (linedef.End, single frontSideSector.CeilingHeight)
                                |]
                            TextureAlignment = 
                                if not isUpperUnpegged then
                                    LowerUnpegged
                                else
                                    UpperUnpegged 0
                            Special = special
                        }
                        |> arr.Add
                       
                    if backSidedef.LowerTextureName.Contains("-") |> not && frontSideSector.FloorHeight > sector.FloorHeight then
                        let frontSideSector = Seq.item frontSidedef.SectorNumber level.sectors

                        {
                            SectorId = backSidedef.SectorNumber
                            TextureName = Some backSidedef.LowerTextureName
                            TextureOffsetX = backSidedef.OffsetX
                            TextureOffsetY = backSidedef.OffsetY
                            Vertices = 
                                [|
                                    Vector3 (linedef.End, single sector.FloorHeight)
                                    Vector3 (linedef.Start, single sector.FloorHeight)
                                    Vector3 (linedef.Start, single frontSideSector.FloorHeight)

                                    Vector3 (linedef.Start, single frontSideSector.FloorHeight)
                                    Vector3 (linedef.End, single frontSideSector.FloorHeight)
                                    Vector3 (linedef.End, single sector.FloorHeight)
                                |]
                            TextureAlignment = 
                                if isLowerUnpegged then
                                    if isTwoSided then
                                        UpperUnpegged (abs (sector.CeilingHeight - frontSideSector.FloorHeight))
                                    else
                                        LowerUnpegged
                                else
                                    UpperUnpegged 0
                            Special = special
                        } |> arr.Add
            
                | _ -> ()

                if backSidedef.MiddleTextureName.Contains("-") |> not then

                    match linedef.FrontSidedef with
                    | Some frontSidedef ->
                        let frontSideSector = Seq.item frontSidedef.SectorNumber level.sectors

                        addMiddleBack sector.FloorHeight frontSideSector.CeilingHeight backSidedef
                    | _ -> ()

            | _ -> ()

            match linedef.FrontSidedef with
            | Some frontSidedef when frontSidedef.SectorNumber = sectorId ->

                match linedef.BackSidedef with
                | Some backSidedef ->
                    if frontSidedef.UpperTextureName.Contains("-") |> not then
                        let backSideSector = Seq.item backSidedef.SectorNumber level.sectors

                        {
                            SectorId = frontSidedef.SectorNumber
                            TextureName = Some frontSidedef.UpperTextureName
                            TextureOffsetX = frontSidedef.OffsetX
                            TextureOffsetY = frontSidedef.OffsetY
                            Vertices =
                                [|
                                    Vector3 (linedef.Start, single backSideSector.CeilingHeight)
                                    Vector3 (linedef.End, single backSideSector.CeilingHeight)
                                    Vector3 (linedef.End, single sector.CeilingHeight)

                                    Vector3 (linedef.End, single sector.CeilingHeight)
                                    Vector3 (linedef.Start, single sector.CeilingHeight)
                                    Vector3 (linedef.Start, single backSideSector.CeilingHeight)
                                |]
                            TextureAlignment = 
                                if not isUpperUnpegged then
                                    LowerUnpegged
                                else
                                    UpperUnpegged 0
                            Special = special
                        }
                        |> arr.Add

                    if frontSidedef.LowerTextureName.Contains("-") |> not then
                        let backSideSector = Seq.item backSidedef.SectorNumber level.sectors

                        {
                            SectorId = frontSidedef.SectorNumber
                            TextureName = Some frontSidedef.LowerTextureName
                            TextureOffsetX = frontSidedef.OffsetX
                            TextureOffsetY = frontSidedef.OffsetY
                            Vertices = 
                                [|
                                    Vector3 (linedef.End, single backSideSector.FloorHeight)
                                    Vector3 (linedef.Start, single backSideSector.FloorHeight)
                                    Vector3 (linedef.Start, single sector.FloorHeight)

                                    Vector3 (linedef.Start, single sector.FloorHeight)
                                    Vector3 (linedef.End, single sector.FloorHeight)
                                    Vector3 (linedef.End, single backSideSector.FloorHeight)
                                |]
                            TextureAlignment = 
                                if isLowerUnpegged then
                                    if isTwoSided then
                                        UpperUnpegged (abs (sector.CeilingHeight - backSideSector.FloorHeight))
                                    else
                                        LowerUnpegged
                                else
                                    UpperUnpegged 0
                            Special = special
                        } |> arr.Add

                | _ -> ()

                if frontSidedef.MiddleTextureName.Contains("-") |> not then

                    let floorHeight =
                        match linedef.BackSidedef with
                        | Some backSidedef -> 
                            let backSideSector = Seq.item backSidedef.SectorNumber level.sectors
                            backSideSector.FloorHeight
                        | _ -> sector.FloorHeight
                       
                    addMiddleFront floorHeight sector.CeilingHeight frontSidedef

            | _ -> ()
        )

        arr :> IEnumerable<Wall>
