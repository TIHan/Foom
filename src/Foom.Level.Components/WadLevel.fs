namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry
open Foom.Wad

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module WadLevel =

    open Foom.Wad

    let createWalls level =
        let arr = ResizeArray<Wall> ()
        //let sector = level |> Level.getSector sectorId

        //sector.Linedefs
        //(sectorId, level)
        //||> Level.iterLinedefBySectorId (fun linedef ->
        level
        |> Level.iterLinedef (fun linedef ->
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
            | Some backSidedef ->

                let backSideSector = level |> Level.getSector backSidedef.SectorNumber

                match linedef.FrontSidedef with
                | Some frontSidedef ->
                    let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                    if frontSideSector.CeilingHeight < backSideSector.CeilingHeight then

                        upperBack <-
                            {
                                TextureName = backSidedef.UpperTextureName
                                TextureOffsetX = backSidedef.OffsetX
                                TextureOffsetY = backSidedef.OffsetY
                                Vertices =
                                    [|
                                        Vector3 (linedef.End, single frontSideSector.CeilingHeight)
                                        Vector3 (linedef.Start, single frontSideSector.CeilingHeight)
                                        Vector3 (linedef.Start, single backSideSector.CeilingHeight)

                                        Vector3 (linedef.Start, single backSideSector.CeilingHeight)
                                        Vector3 (linedef.End, single backSideSector.CeilingHeight)
                                        Vector3 (linedef.End, single frontSideSector.CeilingHeight)
                                    |]
                                TextureAlignment = 
                                    if not isUpperUnpegged then
                                        LowerUnpegged
                                    else
                                        UpperUnpegged 0
                            } |> Some
                       
                    if frontSideSector.FloorHeight > backSideSector.FloorHeight then
                        let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                        lowerBack <-
                            {
                                TextureName = backSidedef.LowerTextureName
                                TextureOffsetX = backSidedef.OffsetX
                                TextureOffsetY = backSidedef.OffsetY
                                Vertices = 
                                    [|
                                        Vector3 (linedef.End, single backSideSector.FloorHeight)
                                        Vector3 (linedef.Start, single backSideSector.FloorHeight)
                                        Vector3 (linedef.Start, single frontSideSector.FloorHeight)

                                        Vector3 (linedef.Start, single frontSideSector.FloorHeight)
                                        Vector3 (linedef.End, single frontSideSector.FloorHeight)
                                        Vector3 (linedef.End, single backSideSector.FloorHeight)
                                    |]
                                TextureAlignment = 
                                    if isLowerUnpegged then
                                        if isTwoSided then
                                            UpperUnpegged (abs (backSideSector.CeilingHeight - frontSideSector.FloorHeight))
                                        else
                                            LowerUnpegged
                                    else
                                        UpperUnpegged 0
                            } |> Some
            
                | _ -> ()

                if true then

                    let floorHeight, ceilingHeight =
                        match linedef.FrontSidedef with
                        | Some frontSidedef ->
                            let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                            (
                                (
                                    if frontSideSector.FloorHeight > backSideSector.FloorHeight then
                                        frontSideSector.FloorHeight
                                    else
                                        backSideSector.FloorHeight
                                ),
                                (
                                    if frontSideSector.CeilingHeight < backSideSector.CeilingHeight then
                                        frontSideSector.CeilingHeight
                                    else
                                        backSideSector.CeilingHeight
                                )
                            )

                        | _ -> backSideSector.FloorHeight, backSideSector.CeilingHeight

                    addMiddleBack floorHeight ceilingHeight backSidedef

            | _ -> ()

            match linedef.FrontSidedef with
            | Some frontSidedef ->
                let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                match linedef.BackSidedef with
                | Some backSidedef ->
                    let backSideSector = level |> Level.getSector backSidedef.SectorNumber

                    if frontSideSector.CeilingHeight > backSideSector.CeilingHeight then

                        upperFront <-
                            {
                                TextureName = frontSidedef.UpperTextureName
                                TextureOffsetX = frontSidedef.OffsetX
                                TextureOffsetY = frontSidedef.OffsetY
                                Vertices =
                                    [|
                                        Vector3 (linedef.Start, single backSideSector.CeilingHeight)
                                        Vector3 (linedef.End, single backSideSector.CeilingHeight)
                                        Vector3 (linedef.End, single frontSideSector.CeilingHeight)

                                        Vector3 (linedef.End, single frontSideSector.CeilingHeight)
                                        Vector3 (linedef.Start, single frontSideSector.CeilingHeight)
                                        Vector3 (linedef.Start, single backSideSector.CeilingHeight)
                                    |]
                                TextureAlignment = 
                                    if not isUpperUnpegged then
                                        LowerUnpegged
                                    else
                                        UpperUnpegged 0
                            } |> Some

                    if frontSideSector.FloorHeight < backSideSector.FloorHeight then

                        lowerFront <-
                            {
                                TextureName = frontSidedef.LowerTextureName
                                TextureOffsetX = frontSidedef.OffsetX
                                TextureOffsetY = frontSidedef.OffsetY
                                Vertices = 
                                    [|
                                        Vector3 (linedef.End, single backSideSector.FloorHeight)
                                        Vector3 (linedef.Start, single backSideSector.FloorHeight)
                                        Vector3 (linedef.Start, single frontSideSector.FloorHeight)

                                        Vector3 (linedef.Start, single frontSideSector.FloorHeight)
                                        Vector3 (linedef.End, single frontSideSector.FloorHeight)
                                        Vector3 (linedef.End, single backSideSector.FloorHeight)
                                    |]
                                TextureAlignment = 
                                    if isLowerUnpegged then
                                        if isTwoSided then
                                            UpperUnpegged (abs (frontSideSector.CeilingHeight - backSideSector.FloorHeight))
                                        else
                                            LowerUnpegged
                                    else
                                        UpperUnpegged 0
                            } |> Some

                | _ -> ()

                if true then

                    let floorHeight, ceilingHeight =
                        match linedef.BackSidedef with
                        | Some backSidedef ->
                            let backSideSector = level |> Level.getSector backSidedef.SectorNumber

                            (
                                (
                                    if backSideSector.FloorHeight > frontSideSector.FloorHeight then
                                        backSideSector.FloorHeight
                                    else
                                        frontSideSector.FloorHeight
                                ),
                                (
                                    if backSideSector.CeilingHeight < frontSideSector.CeilingHeight then
                                        backSideSector.CeilingHeight
                                    else
                                        frontSideSector.CeilingHeight
                                )
                            )

                        | _ -> frontSideSector.FloorHeight, frontSideSector.CeilingHeight
                       
                    addMiddleFront floorHeight ceilingHeight frontSidedef

            | _ -> ()

            arr.Add
                {
                    Special = special
                    FrontSide =
                        if linedef.FrontSidedef.IsSome then
                            {
                                SectorId = linedef.FrontSidedef.Value.SectorNumber
                                Upper = upperFront
                                Middle = middleFront
                                Lower = lowerFront
                            } |> Some
                        else
                            None

                    BackSide =
                        if linedef.BackSidedef.IsSome then
                            {
                                SectorId = linedef.BackSidedef.Value.SectorNumber
                                Upper = upperBack
                                Middle = middleBack
                                Lower = lowerBack
                            } |> Some
                        else
                            None
                }
        )

        arr :> IEnumerable<Wall>

    let createFlats sectorId level = 
        match level |> Level.tryGetSector sectorId with
        | None -> Seq.empty
        | Some sector ->

            match LinedefTracer.run sectorId level with
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

                try
                    let sectorTriangles =
                        map linedefPolygons
                        |> Seq.map (fun t ->
                                EarClipping.computeTree t
                        )
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

                with | _ ->
                    System.Diagnostics.Debug.WriteLine("Unable to triangulate a polygon in sector: " + sectorId.ToString())
                    Seq.empty