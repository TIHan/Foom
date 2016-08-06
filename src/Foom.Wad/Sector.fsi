namespace Foom.Wad.Level.Structures

open Foom.Wad.Geometry

type Sector = 
    {
        Linedefs: Linedef [] 
        FloorTextureName: string
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =
    val polygonFlats : Sector -> Polygon list