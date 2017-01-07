namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry

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
