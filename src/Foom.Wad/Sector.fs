namespace Foom.Wad.Level.Structures

open System.Numerics

open Foom.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = 
    {
        linedefs: Linedef []
        floorTextureName: string
        floorHeight: int
        ceilingTextureName: string
        ceilingHeight: int
        lightLevel: int
    } 

    member this.Linedefs = this.linedefs |> Seq.ofArray

    member this.FloorTextureName = this.floorTextureName

    member this.FloorHeight = this.floorHeight

    member this.CeilingTextureName = this.ceilingTextureName

    member this.CeilingHeight = this.ceilingHeight

    member this.LightLevel = this.lightLevel
