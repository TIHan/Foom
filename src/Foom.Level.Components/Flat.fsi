namespace Foom.Level

open Foom.Wad.Level

type Flat =
    {
        SectorId: int
        Ceiling: FlatPart
        Floor: FlatPart
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Flat =

    val createFlats : sectorId: int -> Level -> Flat seq
