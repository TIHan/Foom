namespace Foom.Wad.Level.Structures

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