namespace Foom.Wad.Level.Structures

open Foom.Wad.Geometry

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
    val polygonFlats : Sector -> Foom.Wad.Geometry.Triangulation.EarClipping.Triangle2D [] list