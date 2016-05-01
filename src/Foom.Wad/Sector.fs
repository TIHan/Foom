namespace Foom.Wad.Level.Structures

open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = {
    Linedefs: Linedef [] }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =
    let polygonFlats sector = 
        LinedefTracer.run (sector.Linedefs)
        |> List.map (Polygon.ofLinedefs)