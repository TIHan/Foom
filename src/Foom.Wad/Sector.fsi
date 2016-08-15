namespace Foom.Wad.Level.Structures

open System.Numerics

open Foom.Wad.Geometry

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

type RenderLinedef =
    {
        TextureName: string
        TextureOffsetX: int
        TextureOffsetY: int
        Vertices: Vector3 []
        IsMiddle: bool
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    val wallTriangles : Sector seq -> Sector -> ResizeArray<RenderLinedef>

    val polygonFlats : Sector -> Foom.Wad.Geometry.Triangulation.EarClipping.Triangle2D [] list