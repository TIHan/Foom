namespace Foom.Wad.Level.Structures

open System.Numerics

open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = 
    {
        Linedefs: Linedef [] 
        FloorTextureName: string
        FloorHeight: int
        CeilingTextureName: string
        CeilingHeight: int
        LightLevel: int
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    let wallTriangles sector =
        sector.Linedefs
        |> Array.choose (fun linedef ->
            match linedef.FrontSidedef with
            | Some frontSidedef ->

                match linedef.BackSidedef with
                | Some backSidedef ->
                    None
                | _ ->



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
                        ) |> Some
                    else None

            | _ -> None
        )

    let polygonFlats sector = 
        match LinedefTracer.run2 (sector.Linedefs) with
        | [] -> []
        | linedefPolygons ->
            let rec map (linedefPolygons: LinedefPolygon list) =
                linedefPolygons
                |> List.map (fun x -> 
                    {
                        Polygon = x.Linedefs |> Polygon.ofLinedefs
                        Children = map x.Inner
                    }
                )

            map linedefPolygons
            |> List.map Foom.Wad.Geometry.Triangulation.EarClipping.computeTree
            |> List.reduce (@)