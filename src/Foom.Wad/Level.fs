namespace Foom.Wad

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry

type Level =
    {
        sectors: Sector []
        things: Thing []
        linedefs: Linedef ResizeArray
        linedefLookup: Dictionary<int, Linedef ResizeArray>
    }

    static member Create (sectors: Sector seq, things: Thing seq, linedefs, linedefLookup) =
        {
            sectors = sectors |> Array.ofSeq
            things = things |> Array.ofSeq
            linedefs = linedefs
            linedefLookup = linedefLookup
        }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    let iterLinedef f level =
        level.linedefs |> Seq.iter f

    let iterLinedefBySectorId f sectorId level =
        level.linedefLookup.[sectorId] |> Seq.iter f

    let getSector index level =
        level.sectors.[index]

    let tryGetSector index level =
        level.sectors
        |> Seq.tryItem index

    let iteriSector f level =
        level.sectors
        |> Array.iteri f

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

