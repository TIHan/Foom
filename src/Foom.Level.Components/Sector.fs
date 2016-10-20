namespace Foom.Level

type Sector =
    {
        Walls: Wall list
        Flat: Flat
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    let changeCeilingHeight height sector =
        sector.Flat.Ceiling |> FlatPart.changeHeight height
