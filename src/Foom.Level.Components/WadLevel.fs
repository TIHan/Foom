namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry
open Foom.Wad

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module WadLevel =

    open Foom.Wad

    type WallSection =
        | Upper
        | Middle
        | Lower

    let mapToWallPart (level: Foom.Wad.Level) (linedef: Linedef) (sidedef: Sidedef) (isFrontSide: bool) (section: WallSection) (texName: string option) : WallPart option =
        let isLowerUnpegged = linedef.Flags.HasFlag(LinedefFlags.LowerTextureUnpegged)
        let isUpperUnpegged = linedef.Flags.HasFlag(LinedefFlags.UpperTextureUnpegged)

        let isTwoSided = linedef.FrontSidedef.IsSome && linedef.BackSidedef.IsSome

        {
            TextureOffsetX = sidedef.OffsetX
            TextureOffsetY = sidedef.OffsetY
            TextureName = texName
            Vertices = [||]
            TextureAlignment =
                match section with
                | Upper ->
                    if not isUpperUnpegged then
                        LowerUnpegged
                    else
                        UpperUnpegged 0
                | _ ->
                    if isLowerUnpegged then
                        if isTwoSided && section = WallSection.Lower then
                            let frontSideSector = 
                                level
                                |> Foom.Wad.Level.getSector linedef.FrontSidedef.Value.SectorNumber

                            let backSideSector =
                                level
                                |> Foom.Wad.Level.getSector linedef.BackSidedef.Value.SectorNumber


                            if isFrontSide then
                                UpperUnpegged (abs (frontSideSector.CeilingHeight - backSideSector.FloorHeight))
                            else
                                UpperUnpegged (abs (backSideSector.CeilingHeight - frontSideSector.FloorHeight))
                        else
                            LowerUnpegged
                    else
                        UpperUnpegged 0
        }  |> Some

    let mapToWallSide level (linedef: Linedef) isFrontSide (sidedef: Sidedef) : WallSide option =
        {
            SectorId = sidedef.SectorNumber
            Upper =
                mapToWallPart level linedef sidedef isFrontSide WallSection.Upper sidedef.UpperTextureName
            Middle =
                mapToWallPart level linedef sidedef isFrontSide WallSection.Middle sidedef.MiddleTextureName
            Lower =
                mapToWallPart level linedef sidedef isFrontSide WallSection.Lower sidedef.LowerTextureName
        } |> Some

    let toWalls (level: Foom.Wad.Level) =
        let arr = ResizeArray<Wall> ()

        level
        |> Level.iterLinedef (fun linedef ->
            {
                Segment = LineSegment2D (linedef.Start, linedef.End)
                Special = Nothing // TODO
                FrontSide =
                    linedef.FrontSidedef
                    |> Option.bind (mapToWallSide level linedef true)
                BackSide =
                    linedef.BackSidedef
                    |> Option.bind (mapToWallSide level linedef false)
            }
            |> arr.Add
        )

        arr

    let toSectors (level: Foom.Wad.Level) =
        let arr = ResizeArray<Foom.Level.Sector> ()

        level
        |> Level.iteriSector (fun i sector ->
            let s =
                {
                    lightLevel = sector.LightLevel
                    floorHeight = sector.FloorHeight
                    ceilingHeight = sector.CeilingHeight
                    floorTextureName = sector.FloorTextureName
                    ceilingTextureName = sector.CeilingTextureName
                }
            arr.Add s
        )

        arr

    let toLevel level =
        let walls = toWalls level
        let sectors = toSectors level

        Level.create walls sectors

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