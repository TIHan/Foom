namespace Foom.Game.Level

open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Geometry

[<Sealed>]
type Level () =

    let walls = ResizeArray<Wall> ()
    let wallLookup = Dictionary<int, ResizeArray<int>> ()
    let sectors = ResizeArray<Sector> ()

    member this.AddWall (wall : Wall) =
        let index = walls.Count

        walls.Add wall

        wall.FrontSide
        |> Option.iter (fun frontSide ->
            let sectorId = frontSide.SectorId

            let arr =
                match wallLookup.TryGetValue sectorId with
                | false, _ ->
                    let arr = ResizeArray ()
                    wallLookup.Add(sectorId, arr)
                    arr
                | _, arr -> arr

            arr.Add (index)
        )

        wall.BackSide
        |> Option.iter (fun frontSide ->
            let sectorId = frontSide.SectorId

            let arr =
                match wallLookup.TryGetValue sectorId with
                | false, _ ->
                    let arr = ResizeArray ()
                    wallLookup.Add(sectorId, arr)
                    arr
                | _, arr -> arr

            arr.Add (index)
        )

    member this.AddSector (sector : Sector) =
        sectors.Add sector

    member this.ForEachWall f =
        walls |> Seq.iter f

    member this.ForEachWallBySectorId f sectorId =
        wallLookup.[sectorId]
        |> Seq.iter (fun wallId ->
            f walls.[wallId]
        )

    member this.GetSector index =
        sectors.[index]

    member this.TryGetSector index =
        sectors
        |> Seq.tryItem index
    
    member this.ForEachSector f =
        sectors
        |> Seq.iteri f

    member this.SectorCount = sectors.Count

    member this.LightLevelBySectorId sectorId =
        let sector = sectors.[sectorId]
        let lightLevel = sector.lightLevel
        if lightLevel > 255 then 255uy
        else byte lightLevel

    member this.CreateWallGeometry (wall : Wall) : (Vector3 [] * Vector3 [] * Vector3 []) * (Vector3 [] * Vector3 [] * Vector3 []) =
        let seg = wall.Segment
        let a = seg.A
        let b = seg.B

        // Upper Front
        let mutable upperFront = [||]
        wall.FrontSide
        |> Option.iter (fun frontSide ->
            wall.BackSide
            |> Option.iter (fun backSide ->
                let frontSideSector = this.GetSector frontSide.SectorId
                let backSideSector = this.GetSector backSide.SectorId

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
            let frontSideSector = this.GetSector frontSide.SectorId

            let floorHeight, ceilingHeight =
                match wall.BackSide with
                | Some backSide ->
                    let backSideSector = this.GetSector backSide.SectorId

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
                let frontSideSector = this.GetSector frontSide.SectorId
                let backSideSector = this.GetSector backSide.SectorId

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
                let backSideSector = this.GetSector backSide.SectorId
                let frontSideSector = this.GetSector frontSide.SectorId

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
            let backSideSector = this.GetSector backSide.SectorId

            let floorHeight, ceilingHeight =
                match wall.FrontSide with
                | Some frontSide ->
                    let frontSideSector = this.GetSector frontSide.SectorId

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
                let backSideSector = this.GetSector backSide.SectorId
                let frontSideSector = this.GetSector frontSide.SectorId

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


[<Sealed>]
type LevelComponent (level: Level) =
    inherit Component ()

    member val Level = level
