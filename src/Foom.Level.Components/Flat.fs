namespace Foom.Level

open System.Numerics

open Foom.Geometry

type Flat =
    {
        SectorId: int
        Ceiling: FlatPart
        Floor: FlatPart
    }
