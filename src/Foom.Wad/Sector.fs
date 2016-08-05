namespace Foom.Wad.Level.Structures

open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = {
    Linedefs: Linedef [] }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    let polygonFlats sector = 
        let polygons =
            LinedefTracer.run2 (sector.Linedefs)
            |> List.map (fun x -> Polygon.ofLinedefs x.Linedefs)

        polygons
        |> List.map Foom.Wad.Geometry.Triangulation.EarClipping.compute
        |> List.reduce (@)
        //polygons