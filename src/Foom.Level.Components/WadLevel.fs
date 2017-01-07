namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry
open Foom.Wad

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module WadLevel =

    open Foom.Wad

    let createWalls (sectorId: int) level =
        let arr = ResizeArray<Wall> ()
        let sector = level |> Level.getSector sectorId

        sector.Linedefs
        |> Seq.iter (fun linedef ->
            let isLowerUnpegged = linedef.Flags.HasFlag(LinedefFlags.LowerTextureUnpegged)
            let isUpperUnpegged = linedef.Flags.HasFlag(LinedefFlags.UpperTextureUnpegged)

            let isTwoSided = linedef.FrontSidedef.IsSome && linedef.BackSidedef.IsSome

            let special =
                if linedef.SpecialType = 1 && linedef.BackSidedef.IsSome then
                    Door (linedef.BackSidedef.Value.SectorNumber)
                else
                    Nothing

            let mutable upperFront = None
            let mutable middleFront = None
            let mutable lowerFront = None

            let mutable upperBack = None
            let mutable middleBack = None
            let mutable lowerBack = None

            let createMiddleWithVertices (floorHeight: int) (ceilingHeight: int) (sidedef: Sidedef) vertices =
                {
                    TextureName = sidedef.MiddleTextureName
                    TextureOffsetX = sidedef.OffsetX
                    TextureOffsetY = sidedef.OffsetY
                    Vertices = vertices
                    TextureAlignment =
                        if isLowerUnpegged then
                            LowerUnpegged
                        else
                            UpperUnpegged 0
                } |> Some

            let addMiddleFront floorHeight ceilingHeight sidedef =
                middleFront <- createMiddleWithVertices floorHeight ceilingHeight sidedef
                    [|
                        Vector3 (linedef.Start, single floorHeight)
                        Vector3 (linedef.End, single floorHeight)
                        Vector3 (linedef.End, single ceilingHeight)

                        Vector3 (linedef.End, single ceilingHeight)
                        Vector3 (linedef.Start, single ceilingHeight)
                        Vector3 (linedef.Start, single floorHeight)
                    |]

            let addMiddleBack floorHeight ceilingHeight sidedef =
                middleBack <- createMiddleWithVertices floorHeight ceilingHeight sidedef <|
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
                    let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                    if backSidedef.UpperTextureName.IsSome && frontSideSector.CeilingHeight < sector.CeilingHeight then

                        upperBack <-
                            {
                                TextureName = backSidedef.UpperTextureName
                                TextureOffsetX = backSidedef.OffsetX
                                TextureOffsetY = backSidedef.OffsetY
                                Vertices =
                                    [|
                                        Vector3 (linedef.End, single frontSideSector.CeilingHeight)
                                        Vector3 (linedef.Start, single frontSideSector.CeilingHeight)
                                        Vector3 (linedef.Start, single sector.CeilingHeight)

                                        Vector3 (linedef.Start, single sector.CeilingHeight)
                                        Vector3 (linedef.End, single sector.CeilingHeight)
                                        Vector3 (linedef.End, single frontSideSector.CeilingHeight)
                                    |]
                                TextureAlignment = 
                                    if not isUpperUnpegged then
                                        LowerUnpegged
                                    else
                                        UpperUnpegged 0
                            } |> Some
                       
                    if backSidedef.LowerTextureName.IsSome && frontSideSector.FloorHeight > sector.FloorHeight then
                        let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                        lowerBack <-
                            {
                                TextureName = backSidedef.LowerTextureName
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
                            } |> Some
            
                | _ -> ()

                if backSidedef.MiddleTextureName.IsSome then

                    let floorHeight, ceilingHeight =
                        match linedef.FrontSidedef with
                        | Some frontSidedef ->
                            let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                            (
                                (
                                    if frontSideSector.FloorHeight > sector.FloorHeight then
                                        frontSideSector.FloorHeight
                                    else
                                        sector.FloorHeight
                                ),
                                (
                                    if frontSideSector.CeilingHeight < sector.CeilingHeight then
                                        frontSideSector.CeilingHeight
                                    else
                                        sector.CeilingHeight
                                )
                            )

                        | _ -> sector.FloorHeight, sector.CeilingHeight

                    addMiddleBack floorHeight ceilingHeight backSidedef

            | _ -> ()

            match linedef.FrontSidedef with
            | Some frontSidedef when frontSidedef.SectorNumber = sectorId ->

                match linedef.BackSidedef with
                | Some backSidedef ->
                    let backSideSector = level |> Level.getSector backSidedef.SectorNumber

                    if frontSidedef.UpperTextureName.IsSome && sector.CeilingHeight > backSideSector.CeilingHeight then

                        upperFront <-
                            {
                                TextureName = frontSidedef.UpperTextureName
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
                            } |> Some

                    if frontSidedef.LowerTextureName.IsSome && sector.FloorHeight < backSideSector.FloorHeight then

                        lowerFront <-
                            {
                                TextureName = frontSidedef.LowerTextureName
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
                            } |> Some

                | _ -> ()

                if frontSidedef.MiddleTextureName.IsSome then

                    let floorHeight, ceilingHeight =
                        match linedef.BackSidedef with
                        | Some backSidedef ->
                            let backSideSector = level |> Level.getSector backSidedef.SectorNumber

                            (
                                (
                                    if backSideSector.FloorHeight > sector.FloorHeight then
                                        backSideSector.FloorHeight
                                    else
                                        sector.FloorHeight
                                ),
                                (
                                    if backSideSector.CeilingHeight < sector.CeilingHeight then
                                        backSideSector.CeilingHeight
                                    else
                                        sector.CeilingHeight
                                )
                            )

                        | _ -> sector.FloorHeight, sector.CeilingHeight
                       
                    addMiddleFront floorHeight ceilingHeight frontSidedef

            | _ -> ()

            arr.Add
                {
                    SectorId = sectorId
                    Special = special
                    Upper =
                        {
                            FrontSide = upperFront
                            BackSide = upperBack
                        }
                    Middle =
                        {
                            FrontSide = middleFront
                            BackSide = middleBack
                        }
                    Lower =
                        {
                            FrontSide = lowerFront
                            BackSide = lowerBack
                        }
                }
        )

        arr :> IEnumerable<Wall>

    let createFlats sectorId level = 
        match level |> Level.tryGetSector sectorId with
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
                    |> Seq.map (EarClipping.computeTree)
                    |> Seq.reduce Seq.append
                    |> Array.ofSeq

                sectorTriangles
                |> Seq.filter (fun x -> x.Length <> 0)
                |> Seq.map (fun triangles ->
                  
                    let ceiling =
                        FlatPart.create
                            (
                                triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.C.X, tri.C.Y, single sector.CeilingHeight)
                                        Vector3 (tri.B.X, tri.B.Y, single sector.CeilingHeight)
                                        Vector3 (tri.A.X, tri.A.Y, single sector.CeilingHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append
                            )
                            (float32 sector.CeilingHeight)
                            (Some sector.CeilingTextureName)

                    let floor =
                        FlatPart.create
                            (
                                triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.A.X, tri.A.Y, single sector.FloorHeight)
                                        Vector3 (tri.B.X, tri.B.Y, single sector.FloorHeight)
                                        Vector3 (tri.C.X, tri.C.Y, single sector.FloorHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append
                            )
                            (float32 sector.FloorHeight)
                            (Some sector.FloorTextureName)

                    {
                        SectorId = sectorId
                        Ceiling = ceiling
                        Floor = floor
                    }
                )