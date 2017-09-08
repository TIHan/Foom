namespace Foom.Game.Level

open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Geometry

type Sector =
    {
        mutable lightLevel: int
        mutable floorHeight: int
        mutable ceilingHeight: int

        mutable floorTextureName: string
        mutable ceilingTextureName: string
    }

[<AutoOpen>]
module private LevelHelpers =

    let updateUpperFront (a : Vector2) (b : Vector2) (wall : Wall) getSector (data : Vector3 []) =
        wall.FrontSide
        |> Option.iter (fun frontSide ->
            wall.BackSide
            |> Option.iter (fun backSide ->
                let frontSideSector = getSector frontSide.SectorId
                let backSideSector = getSector backSide.SectorId

                if frontSideSector.ceilingHeight > backSideSector.ceilingHeight then

                    let floorHeight, ceilingHeight = backSideSector.ceilingHeight, frontSideSector.ceilingHeight

                    data.[0] <- Vector3 (a, single floorHeight)
                    data.[1] <- Vector3 (b, single floorHeight)
                    data.[2] <- Vector3 (b, single ceilingHeight)

                    data.[3] <- Vector3 (b, single ceilingHeight)
                    data.[4] <- Vector3 (a, single ceilingHeight)
                    data.[5] <- Vector3 (a, single floorHeight)
            )
        )

    let createUpperFront (a : Vector2) (b : Vector2) (wall : Wall) getSector =
        let data = Array.zeroCreate 6
        updateUpperFront a b wall getSector data
        data


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

    member this.UpdateSectorCeilingHeight (height : int, sectorId : int, f) =
        let sector = this.GetSector sectorId

        sector.ceilingHeight <- height

        match wallLookup.TryGetValue sectorId with
        | true, indices ->

            for i = 0 to indices.Count - 1 do
                let wall = walls.[indices.[i]]

                match wall.FrontSide with
                | Some frontSide when frontSide.SectorId = sectorId ->
                    f sectorId i true
                | _ -> ()

                match wall.BackSide with
                | Some backSide when backSide.SectorId = sectorId ->
                    f sectorId i false
                | _ -> ()

        | _ -> ()

    member this.CreateWallGeometry (wall : Wall) : (Vector3 [] * Vector3 [] * Vector3 []) * (Vector3 [] * Vector3 [] * Vector3 []) =
        let seg = wall.Segment
        let a = seg.A
        let b = seg.B

        // Upper Front
        let upperFront = createUpperFront a b wall this.GetSector 

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
