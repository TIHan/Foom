namespace Foom.Game.Level

open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Geometry

type Level =
    {
        walls: Wall ResizeArray
        wallLookup: Dictionary<int, int ResizeArray>
        sectors: Sector ResizeArray
    } 

[<Sealed>]
type LevelComponent (level: Level) =
    inherit Component ()

    member val Level = level

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    let create (walls: Wall seq) (sectors: Sector seq) =

        let wallLookup = Dictionary ()

        walls
        |> Seq.iteri (fun i x ->
            x.FrontSide
            |> Option.iter (fun frontSide ->
                let sectorId = frontSide.SectorId

                let arr =
                    match wallLookup.TryGetValue sectorId with
                    | false, _ ->
                        let arr = ResizeArray ()
                        wallLookup.Add(sectorId, arr)
                        arr
                    | _, arr -> arr

                arr.Add (i)
            )

            x.BackSide
            |> Option.iter (fun frontSide ->
                let sectorId = frontSide.SectorId

                let arr =
                    match wallLookup.TryGetValue sectorId with
                    | false, _ ->
                        let arr = ResizeArray ()
                        wallLookup.Add(sectorId, arr)
                        arr
                    | _, arr -> arr

                arr.Add (i)
            )
        )

        {
            walls = ResizeArray (walls)
            wallLookup = wallLookup
            sectors = ResizeArray (sectors)
        }

    let iterWall f level =
        level.walls |> Seq.iter f

    let iterWallBySectorId f sectorId level =
        level.wallLookup.[sectorId]
        |> Seq.iter (fun wallId ->
            f level.walls.[wallId]
        )

    let getSector index level =
        level.sectors.[index]

    let tryGetSector index level =
        level.sectors
        |> Seq.tryItem index

    let iteriSector f level =
        level.sectors
        |> Seq.iteri f

    let getSectorCount level = level.sectors.Count

    let lightLevelBySectorId sectorId (level: Level) =
        let sector = level.sectors.[sectorId]
        let lightLevel = sector.lightLevel
        if lightLevel > 255 then 255uy
        else byte lightLevel

    let createWallGeometry (wall: Wall) (level: Level) : (Vector3 [] * Vector3 [] * Vector3 []) * (Vector3 [] * Vector3 [] * Vector3 [])  =
        let seg = wall.Segment
        let a = seg.A
        let b = seg.B

        // Upper Front
        let mutable upperFront = [||]
        wall.FrontSide
        |> Option.iter (fun frontSide ->
            wall.BackSide
            |> Option.iter (fun backSide ->
                let frontSideSector = level |> getSector frontSide.SectorId
                let backSideSector = level |> getSector backSide.SectorId

                if frontSideSector.ceilingHeight > backSideSector.ceilingHeight then

                    let floorHeight, ceilingHeight = backSideSector.ceilingHeight, frontSideSector.ceilingHeight

                    upperFront <-
                        [|
                            Vector3 (a, single floorHeight)
                            Vector3 (b, single floorHeight)
                            Vector3 (b, single ceilingHeight)

                            Vector3 (b, single ceilingHeight)
                            Vector3 (a, single ceilingHeight)
                            Vector3 (a, single floorHeight)
                        |]
            )
        )

        // Middle Front
        let mutable middleFront = [||]
        wall.FrontSide
        |> Option.iter (fun frontSide ->
            let frontSideSector = level |> getSector frontSide.SectorId

            let floorHeight, ceilingHeight =
                match wall.BackSide with
                | Some backSide ->
                    let backSideSector = level |> getSector backSide.SectorId

                    (
                        (
                            if backSideSector.floorHeight > frontSideSector.floorHeight then
                                backSideSector.floorHeight
                            else
                                frontSideSector.floorHeight
                        ),
                        (
                            if backSideSector.ceilingHeight < frontSideSector.ceilingHeight then
                                backSideSector.ceilingHeight
                            else
                                frontSideSector.ceilingHeight
                        )
                    )

                | _ -> 
                    frontSideSector.floorHeight, frontSideSector.ceilingHeight

            middleFront <-
                [|
                    Vector3 (a, single floorHeight)
                    Vector3 (b, single floorHeight)
                    Vector3 (b, single ceilingHeight)

                    Vector3 (b, single ceilingHeight)
                    Vector3 (a, single ceilingHeight)
                    Vector3 (a, single floorHeight)
                |]
        )

        // Lower Front
        let mutable lowerFront = [||]
        wall.FrontSide
        |> Option.iter (fun frontSide ->
            wall.BackSide
            |> Option.iter (fun backSide ->
                let frontSideSector = level |> getSector frontSide.SectorId
                let backSideSector = level |> getSector backSide.SectorId

                if frontSideSector.floorHeight < backSideSector.floorHeight then

                    let floorHeight, ceilingHeight = frontSideSector.floorHeight, backSideSector.floorHeight

                    lowerFront <-
                        [|
                            Vector3 (a, single floorHeight)
                            Vector3 (b, single floorHeight)
                            Vector3 (b, single ceilingHeight)

                            Vector3 (b, single ceilingHeight)
                            Vector3 (a, single ceilingHeight)
                            Vector3 (a, single floorHeight)
                        |]
            )
        )

        // Upper Back
        let mutable upperBack = [||]
        wall.BackSide
        |> Option.iter (fun backSide ->
            wall.FrontSide
            |> Option.iter (fun frontSide ->
                let backSideSector = level |> getSector backSide.SectorId
                let frontSideSector = level |> getSector frontSide.SectorId

                if frontSideSector.ceilingHeight < backSideSector.ceilingHeight then

                    let floorHeight, ceilingHeight = frontSideSector.ceilingHeight, backSideSector.ceilingHeight

                    upperBack <-
                        [|
                            Vector3 (b, single floorHeight)
                            Vector3 (a, single floorHeight)
                            Vector3 (a, single ceilingHeight)

                            Vector3 (a, single ceilingHeight)
                            Vector3 (b, single ceilingHeight)
                            Vector3 (b, single floorHeight)
                        |]
            )
        )

        // Middle Back
        let mutable middleBack = [||]
        wall.BackSide
        |> Option.iter (fun backSide ->
            let backSideSector = level |> getSector backSide.SectorId

            let floorHeight, ceilingHeight =
                match wall.FrontSide with
                | Some frontSide ->
                    let frontSideSector = level |> getSector frontSide.SectorId

                    (
                        (
                            if frontSideSector.floorHeight > backSideSector.floorHeight then
                                frontSideSector.floorHeight
                            else
                                backSideSector.floorHeight
                        ),
                        (
                            if frontSideSector.ceilingHeight < backSideSector.ceilingHeight then
                                frontSideSector.ceilingHeight
                            else
                                backSideSector.ceilingHeight
                        )
                    )

                | _ -> backSideSector.floorHeight, backSideSector.ceilingHeight

            middleBack <-
                [|
                    Vector3 (b, single floorHeight)
                    Vector3 (a, single floorHeight)
                    Vector3 (a, single ceilingHeight)

                    Vector3 (a, single ceilingHeight)
                    Vector3 (b, single ceilingHeight)
                    Vector3 (b, single floorHeight)
                |]
        )

        // Lower Front
        let mutable lowerBack = [||]
        wall.BackSide
        |> Option.iter (fun backSide ->
            wall.FrontSide
            |> Option.iter (fun frontSide ->
                let backSideSector = level |> getSector backSide.SectorId
                let frontSideSector = level |> getSector frontSide.SectorId

                if frontSideSector.floorHeight > backSideSector.floorHeight then

                    let floorHeight, ceilingHeight = backSideSector.floorHeight, frontSideSector.floorHeight

                    lowerBack <-
                        [|
                            Vector3 (b, single floorHeight)
                            Vector3 (a, single floorHeight)
                            Vector3 (a, single ceilingHeight)

                            Vector3 (a, single ceilingHeight)
                            Vector3 (b, single ceilingHeight)
                            Vector3 (b, single floorHeight)
                        |]
            )
        )

        (
            (upperFront, middleFront, lowerFront),
            (upperBack, middleBack, lowerBack)
        )
