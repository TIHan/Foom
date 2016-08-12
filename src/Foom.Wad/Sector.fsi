namespace Foom.Wad.Level.Structures

open System.Numerics

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

    val wallTriangles : Sector -> Vector3 [] list

    val polygonFlats : Sector -> Foom.Wad.Geometry.Triangulation.EarClipping.Triangle2D [] list