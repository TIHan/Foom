namespace Foom.Wad.Level.Structures

open System.Numerics

open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = 
    {
        Id: int
        Linedefs: Linedef [] 
        FloorTextureName: string
        FloorHeight: int
        CeilingTextureName: string
        CeilingHeight: int
        LightLevel: int
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    let wallTriangles (sectors: Sector seq) sector =
        let arr = ResizeArray<string * Vector3 []> ()

        sector.Linedefs
        |> Array.iter (fun linedef ->
            match linedef.FrontSidedef with
            | Some frontSidedef when not (linedef.Flags.HasFlag(LinedefFlags.NerverShowsOnAutomap))  ->

                match linedef.BackSidedef with
                | Some backSidedef ->

                    if frontSidedef.UpperTextureName.Contains("-") |> not then
                        let backSideSector = Seq.item backSidedef.SectorNumber sectors

                        (
                            frontSidedef.UpperTextureName,
                            [|
                                Vector3 (linedef.Start, single backSideSector.CeilingHeight)
                                Vector3 (linedef.End, single backSideSector.CeilingHeight)
                                Vector3 (linedef.End, single sector.CeilingHeight)

                                Vector3 (linedef.End, single sector.CeilingHeight)
                                Vector3 (linedef.Start, single sector.CeilingHeight)
                                Vector3 (linedef.Start, single backSideSector.CeilingHeight)
                            |]
                        ) |> arr.Add

                    if frontSidedef.LowerTextureName.Contains("-") |> not then
                        let backSideSector = Seq.item backSidedef.SectorNumber sectors

                        (
                            frontSidedef.LowerTextureName,
                            [|
                                Vector3 (linedef.End, single backSideSector.FloorHeight)
                                Vector3 (linedef.Start, single backSideSector.FloorHeight)
                                Vector3 (linedef.Start, single sector.FloorHeight)

                                Vector3 (linedef.Start, single sector.FloorHeight)
                                Vector3 (linedef.End, single sector.FloorHeight)
                                Vector3 (linedef.End, single backSideSector.FloorHeight)
                            |]
                        ) |> arr.Add

                | _ -> ()



                if frontSidedef.MiddleTextureName.Contains("-") |> not then
                    (
                        frontSidedef.MiddleTextureName,
                        [|
                            Vector3 (linedef.Start, single sector.FloorHeight)
                            Vector3 (linedef.End, single sector.FloorHeight)
                            Vector3 (linedef.End, single sector.CeilingHeight)

                            Vector3 (linedef.End, single sector.CeilingHeight)
                            Vector3 (linedef.Start, single sector.CeilingHeight)
                            Vector3 (linedef.Start, single sector.FloorHeight)
                        |]
                    )
                    |> arr.Add
            | _ -> ()
        )

        arr

    let polygonFlats sector = 
        match LinedefTracer.run2 (sector.Linedefs) sector.Id with
        | [] -> []
        | linedefPolygons ->
            let rec map (linedefPolygons: LinedefPolygon list) =
                linedefPolygons
                |> List.map (fun x -> 
                    {
                        Polygon = (x.Linedefs, sector.Id) ||> Polygon.ofLinedefs
                        Children = map x.Inner
                    }
                )

            map linedefPolygons
            |> List.map Foom.Wad.Geometry.Triangulation.EarClipping.computeTree
            |> List.reduce (@)