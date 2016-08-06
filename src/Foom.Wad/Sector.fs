namespace Foom.Wad.Level.Structures

open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = 
    {
        Linedefs: Linedef [] 
        FloorTextureName: string
        LightLevel: int
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    let polygonFlats sector = 
        let linedefPolygons = LinedefTracer.run2 (sector.Linedefs)

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