namespace Foom.Wad.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type Level = 
    {
        Sectors: Sector [] 
    }

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

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    let createWalls sector level =
        let arr = ResizeArray<Wall> ()

        sector.Linedefs
        |> Array.iter (fun linedef ->
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
                        let backSideSector = Seq.item backSidedef.SectorNumber level.Sectors

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
                        let backSideSector = Seq.item backSidedef.SectorNumber level.Sectors

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
                            let backSideSector = Seq.item backSidedef.SectorNumber level.Sectors
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