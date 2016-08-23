namespace Foom.Wad.Level.Structures

open System.Numerics

open Foom.Geometry

[<NoComparison; ReferenceEquality>]
type Sector = 
    internal {
        linedefs: Linedef [] 
        floorTextureName: string
        floorHeight: int
        ceilingTextureName: string
        ceilingHeight: int
        lightLevel: int
    } 

    member Linedefs : Linedef seq

    member FloorTextureName : string

    member FloorHeight : int

    member CeilingTextureName : string

    member CeilingHeight : int

    member LightLevel : int
