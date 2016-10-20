namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type WallSpecial =
    | Nothing
    | Door of ceilingSectorId: int

type Wall =
    {
        SectorId: int
        Special: WallSpecial

        Upper: WallPart option
        Middle: WallPart option
        Lower: WallPart option
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wall =

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

            let mutable upper = None
            let mutable middle = None
            let mutable lower = None

            let addMiddleWithVertices (floorHeight: int) (ceilingHeight: int) (sidedef: Sidedef) vertices =
                middle <-
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
                    let frontSideSector = level |> Level.getSector frontSidedef.SectorNumber

                    if backSidedef.UpperTextureName.IsSome && frontSideSector.CeilingHeight < sector.CeilingHeight then

                        upper <-
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

                        lower <-
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

                        upper <-
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

                        lower <-
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
                    Upper = upper
                    Middle = middle
                    Lower = lower
                }
        )

        arr :> IEnumerable<Wall>
