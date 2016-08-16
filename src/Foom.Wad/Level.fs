namespace Foom.Wad.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type Wall =
    {
        TextureName: string
        TextureOffsetX: int
        TextureOffsetY: int
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
    }

type Level =
    {
        sectors: Sector []
    }

    member this.Sectors = this.sectors |> Seq.ofArray

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wall =

    let createUV width height (wall: Wall) =
        let vertices = wall.Vertices
        let uv = Array.zeroCreate vertices.Length

        let mutable i = 0
        while (i < vertices.Length) do
            let p1 = vertices.[i]
            let p2 = vertices.[i + 1]
            let p3 = vertices.[i + 2]

            let width = single width
            let height = single height

            let v1 = Vector2 (p1.X, p1.Y)
            let v2 = Vector2 (p2.X, p2.Y)
            let v3 = Vector2 (p3.X, p3.Y)

            let one = 0.f + single wall.TextureOffsetX
            let two = (v2 - v1).Length ()

            let x, y, z1, z3 =

                // lower unpeg
                match wall.TextureAlignment with
                | LowerUnpegged ->
                    let ofsY = single wall.TextureOffsetY / height * -1.f
                    if p3.Z < p1.Z then
                        (one + two) / width, 
                        one / width, 
                        0.f - ofsY,
                        ((abs (p1.Z - p3.Z)) / height * -1.f) - ofsY
                    else
                        one / width, 
                        (one + two) / width, 
                        ((abs (p1.Z - p3.Z)) / height * -1.f) - ofsY,
                        0.f - ofsY

                // upper unpeg
                | UpperUnpegged offsetY ->
                    let z = single offsetY / height * -1.f
                    let ofsY = single wall.TextureOffsetY / height * -1.f
                    if p3.Z < p1.Z then
                        (one + two) / width, 
                        one / width, 
                        (1.f - ((abs (p1.Z - p3.Z)) / height * -1.f)) - z - ofsY,
                        1.f - z - ofsY
                    else
                        one / width, 
                        (one + two) / width, 
                        1.f - z - ofsY,
                        (1.f - ((abs (p1.Z - p3.Z)) / height * -1.f)) - z - ofsY

            

            uv.[i] <- Vector2 (x, z3)
            uv.[i + 1] <- Vector2(y, z3)
            uv.[i + 2] <- Vector2(y, z1)

            i <- i + 3
        uv

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    let createWalls (sector: Sector) level =
        let arr = ResizeArray<Wall> ()

        sector.Linedefs
        |> Seq.iter (fun linedef ->
            match linedef.FrontSidedef with
            | Some frontSidedef ->

                let isLowerUnpegged = linedef.Flags.HasFlag(LinedefFlags.LowerTextureUnpegged)
                let isUpperUnpegged = linedef.Flags.HasFlag(LinedefFlags.UpperTextureUnpegged)
                let isTwoSided = linedef.Flags.HasFlag(LinedefFlags.TwoSided)

                let addMiddleWithVertices (floorHeight: int) (ceilingHeight: int) (sidedef: Sidedef) vertices =
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
                    } |> arr.Add

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
                | Some backSidedef ->

                    if frontSidedef.UpperTextureName.Contains("-") |> not then
                        let backSideSector = Seq.item backSidedef.SectorNumber level.sectors

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
                        }
                        |> arr.Add

                    if frontSidedef.LowerTextureName.Contains("-") |> not then
                        let backSideSector = Seq.item backSidedef.SectorNumber level.sectors

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
                        } |> arr.Add

                

                | _ -> ()



                if frontSidedef.MiddleTextureName.Contains("-") |> not then

                    let floorHeight =
                        match linedef.BackSidedef with
                        | Some backSidedef -> 
                            let backSideSector = Seq.item backSidedef.SectorNumber level.sectors
                            backSideSector.FloorHeight
                        | _ -> sector.FloorHeight
                       
                    addMiddleFront floorHeight sector.CeilingHeight frontSidedef

                    linedef.BackSidedef
                    |> Option.iter (fun backSidedef ->
                        addMiddleBack floorHeight sector.CeilingHeight backSidedef
                    )

            | _ -> ()
        )

        arr :> IEnumerable<Wall>
