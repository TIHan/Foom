namespace Foom.Wad

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry

type Level =
    {
        sectors: Sector []
        mutable sectorPolygons: Polygon2DTree list []
        things: Thing []
    }

    static member Create (sectors: Sector seq, things: Thing seq) =
        {
            sectors = sectors |> Array.ofSeq
            sectorPolygons = Array.empty
            things = things |> Array.ofSeq
        }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    let getAABB level =
        let mutable minX = 0.f
        let mutable minY = 0.f
        let mutable maxX = 0.f
        let mutable maxY = 0.f

        let f linedef =
            if linedef.Start.X < linedef.End.X then
                minX <- linedef.Start.X
                maxX <- linedef.End.X
            else
                minX <- linedef.End.X
                maxX <- linedef.Start.X

            if linedef.Start.Y < linedef.End.Y then
                minX <- linedef.Start.Y
                maxX <- linedef.End.Y
            else
                minX <- linedef.End.Y
                maxX <- linedef.Start.Y

        if level.sectors.Length > 0 then
            if level.sectors.[0].Linedefs.Length > 0 then

                let linedef = level.sectors.[0].Linedefs.[0]

                if linedef.Start.X < linedef.End.X then
                    minX <- linedef.Start.X
                    maxX <- linedef.End.X
                else
                    minX <- linedef.End.X
                    maxX <- linedef.Start.X

                if linedef.Start.Y < linedef.End.Y then
                    minX <- linedef.Start.Y
                    maxX <- linedef.End.Y
                else
                    minX <- linedef.End.Y
                    maxX <- linedef.Start.Y

                level.sectors
                |> Array.iter (fun sector ->
                    sector.Linedefs
                    |> List.iter (fun linedef ->

                        if linedef.Start.X < minX then minX <- linedef.Start.X
                        if linedef.End.X < minX then minX <- linedef.End.X
                        if linedef.Start.X > maxX then maxX <- linedef.Start.X
                        if linedef.End.X > maxX then maxX <- linedef.End.X
                  
                        if linedef.Start.Y < minY then minY <- linedef.Start.Y
                        if linedef.End.Y < minY then minY <- linedef.End.Y
                        if linedef.Start.Y > maxY then maxY <- linedef.Start.Y
                        if linedef.End.Y > maxY then maxY <- linedef.End.Y

                    )
                )

        (Vector2 (minX, minY), Vector2 (maxX, maxY))
        ||> AABB2D.ofMinAndMax

    let getSector index level =
        level.sectors.[index]

    let tryGetSector index level =
        level.sectors
        |> Seq.tryItem index

    let iteriSector f level =
        level.sectors
        |> Array.iteri f

    let loadSectorPolygons level =
        let rec map sectorId (linedefPolygons: LinedefPolygon list) =
            linedefPolygons
            |> List.map (fun x -> 
                {
                    Polygon = (x.Linedefs, sectorId) ||> Polygon.ofLinedefs
                    Children = map sectorId x.Inner
                }
            )

        level.sectorPolygons <-
            level.sectors
            |> Array.map (fun sector -> LinedefTracer.run2 (sector.Linedefs) sector.Id |> map sector.Id)

    let calculateSectorTriangles2D (sector: Sector) level =
        if level.sectorPolygons |> Array.isEmpty then
            loadSectorPolygons level
        
        level.sectorPolygons.[sector.Id]
        |> Seq.map (EarClipping.computeTree)
        |> Seq.reduce Seq.append

    let sectorAt (point: Vector2) (level: Level) =
        if level.sectorPolygons |> Array.isEmpty then
            loadSectorPolygons level

        let rec sectorAt (i: int) =
            if i < level.sectorPolygons.Length then
                let atSector =
                    level.sectorPolygons.[i]
                    |> List.exists (Polygon2DTree.containsPoint point)
                if atSector then
                    Some level.sectors.[i]
                else
                    sectorAt (i + 1)
            else
                None
            
        sectorAt 0

    let getAdjacentSectors sector level =
        sector.Linedefs
        |> List.choose (fun linedef ->
            match linedef.FrontSidedef, linedef.BackSidedef with
            | Some frontSidedef, Some backSidedef when frontSidedef.SectorNumber = sector.Id ->
                Some level.sectors.[backSidedef.SectorNumber]
            | Some frontSidedef, Some backSidedef when backSidedef.SectorNumber = sector.Id ->
                Some level.sectors.[frontSidedef.SectorNumber]
            | _ -> None
        )
        |> List.distinctBy (fun sector -> sector.Id)

    let tryFindPlayer1Start level =
        level.things
        |> Array.tryFind (function
            | Doom doomThing ->
                doomThing.Type = ThingType.Player1Start
            | _ -> false
        )

    let lightLevelBySectorId sectorId (level: Level) =
        let sector = level.sectors.[sectorId]
        let lightLevel = sector.LightLevel
        if lightLevel > 255 then 255uy
        else byte lightLevel

