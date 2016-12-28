namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type WallSpecial =
    | Nothing
    | Door of ceilingSectorId: int

type Wall =
    {
        SectorId: int
        Special: WallSpecial

        Upper: WallPart
        Middle: WallPart
        Lower: WallPart
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wall =

    val createWalls : sectorId: int -> Level -> Wall seq
