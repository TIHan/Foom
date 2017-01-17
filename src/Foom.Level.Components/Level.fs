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

    let createWallGeometry (wall: Wall) (level: Level) : (Vector3 [] * Vector3 [] * Vector3 []) * (Vector3 [] * Vector3 [] * Vector3 [])  =
        let seg = wall.Segment

        (
            ([||], [||], [||]),
            ([||], [||], [||])
        )

    let create (walls: Wall seq) =

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
            sectors = ResizeArray ()
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
