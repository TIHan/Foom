namespace Foom.Level

open System.Numerics
open System.Collections.Generic

type Level =
    {
        walls: Wall ResizeArray
        wallLookup: Dictionary<int, int ResizeArray>
        sectors: Sector ResizeArray
        things: Foom.Wad.Thing ResizeArray // temporary: get rid of it soon
    }

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
            things = ResizeArray ()
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

    let tryFindPlayer1Start level =
        level.things
        |> Seq.tryFind (function
            | Foom.Wad.Doom doomThing ->
                doomThing.Type = Foom.Wad.ThingType.Player1Start
            | _ -> false
        )

    let lightLevelBySectorId sectorId (level: Level) =
        let sector = level.sectors.[sectorId]
        let lightLevel = sector.lightLevel
        if lightLevel > 255 then 255uy
        else byte lightLevel

    let iterThing f level =
        level.things |> Seq.iter f

    let createWallGeometry (wall: Wall) (level: Level) : (Vector3 [] * Vector3 [] * Vector3 []) * (Vector3 [] * Vector3 [] * Vector3 [])  =
        let seg = wall.Segment

        // Upper Front
        let mutable upperFront = [||]
        wall.FrontSide
        |> Option.iter (fun frontSide ->
            let frontSideSector = level |> getSector frontSide.SectorId

            let floorHeight, ceilingHeight =
                match wall.BackSide with
                | Some backSide ->
                    let backSideSector = level |> getSector backSide.SectorId

                    backSideSector.ceilingHeight, frontSideSector.ceilingHeight

                | _ -> frontSideSector.ceilingHeight, frontSideSector.ceilingHeight

            upperFront <-
                [|
                    Vector3 (seg.A, single floorHeight)
                    Vector3 (seg.B, single floorHeight)
                    Vector3 (seg.B, single ceilingHeight)

                    Vector3 (seg.B, single ceilingHeight)
                    Vector3 (seg.A, single ceilingHeight)
                    Vector3 (seg.A, single floorHeight)
                |]
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

                | _ -> frontSideSector.floorHeight, frontSideSector.ceilingHeight

            middleFront <-
                [|
                    Vector3 (seg.A, single floorHeight)
                    Vector3 (seg.B, single floorHeight)
                    Vector3 (seg.B, single ceilingHeight)

                    Vector3 (seg.B, single ceilingHeight)
                    Vector3 (seg.A, single ceilingHeight)
                    Vector3 (seg.A, single floorHeight)
                |]
        )

        // Lower Front
        let mutable lowerFront = [||]
        wall.FrontSide
        |> Option.iter (fun frontSide ->
            let frontSideSector = level |> getSector frontSide.SectorId

            let floorHeight, ceilingHeight =
                match wall.BackSide with
                | Some backSide ->
                    let backSideSector = level |> getSector backSide.SectorId

                    frontSideSector.floorHeight, backSideSector.floorHeight

                | _ -> frontSideSector.floorHeight, frontSideSector.floorHeight

            lowerFront <-
                [|
                    Vector3 (seg.A, single floorHeight)
                    Vector3 (seg.B, single floorHeight)
                    Vector3 (seg.B, single ceilingHeight)

                    Vector3 (seg.B, single ceilingHeight)
                    Vector3 (seg.A, single ceilingHeight)
                    Vector3 (seg.A, single floorHeight)
                |]
        )

        (
            (upperFront, middleFront, lowerFront),
            ([||], [||], [||])
        )