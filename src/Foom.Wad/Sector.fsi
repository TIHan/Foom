namespace Foom.Wad.Level.Structures

open System.Numerics

open Foom.Wad.Geometry

[<NoComparison; ReferenceEquality>]
type Sector = 
    internal {
        id: int
        linedefs: Linedef [] 
        floorTextureName: string
        floorHeight: int
        ceilingTextureName: string
        ceilingHeight: int
        lightLevel: int
    } 

    member Id : int

    member Linedefs : Linedef seq

    member FloorTextureName : string

    member FloorHeight : int

    member CeilingTextureName : string

    member CeilingHeight : int

    member LightLevel : int

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    val polygonFlats : Sector -> Foom.Wad.Geometry.Triangulation.EarClipping.Triangle2D [] list