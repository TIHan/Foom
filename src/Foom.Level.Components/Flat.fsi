namespace Foom.Level

open Foom.Wad.Level

type Flat =
    {
        SectorId: int
        Ceiling: FlatPart
        Floor: FlatPart
    }
