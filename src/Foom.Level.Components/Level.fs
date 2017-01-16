namespace Foom.Level

open System.Collections.Generic

type Level =
    {
        walls: Wall ResizeArray
        wallLookup: Dictionary<int, Wall ResizeArray>
        sectors: Sector ResizeArray
        things: Foom.Wad.Thing ResizeArray // temporary: get rid of it soon
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    let iterWall f level =
        level.walls |> Seq.iter f

    let iterWallBySectorId f sectorId level =
        level.wallLookup.[sectorId] |> Seq.iter f

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
