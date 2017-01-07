namespace Foom.Wad

[<NoComparison; ReferenceEquality>]
type Sidedef = 
    {
        OffsetX: int
        OffsetY: int
        UpperTextureName: string option
        LowerTextureName: string option
        MiddleTextureName: string option
        SectorNumber: int 
    }

