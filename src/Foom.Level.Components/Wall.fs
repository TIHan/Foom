namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry

type WallSpecial =
    | Nothing
    | Door of ceilingSectorId: int

type WallSide =
    {
        SectorId: int

        Upper: WallPart
        Middle: WallPart
        Lower: WallPart
    }

type Wall =
    {
        Segment: LineSegment2D
        Special: WallSpecial
        FrontSide: WallSide option
        BackSide: WallSide option
    }
