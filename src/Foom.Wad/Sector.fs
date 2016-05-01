namespace Foom.Wad.Level.Structures

open Foom.Shared.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = {
    Linedefs: Linedef [] }

module Polygon =
    let ofLinedefs (linedefs: Linedef list) =
        let vertices =
            linedefs
            |> List.map (fun x -> 
                if x.FrontSidedef.IsSome then x.Start else x.End) 
            |> Array.ofList
        Polygon.create vertices.[..vertices.Length - 1]

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =
    let polygonFlats sector = 
        LinedefTracer.run (sector.Linedefs)
        |> List.map (Polygon.ofLinedefs)