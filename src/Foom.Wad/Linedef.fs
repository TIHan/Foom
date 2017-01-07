namespace Foom.Wad

open System
open System.Numerics

[<Flags>]
type LinedefFlags =
    | Empty = 0x0000
    | BlocksPlayersAndMonsters = 0x0001
    | BlocksMonsters = 0x0002
    | TwoSided = 0x0004
    | UpperTextureUnpegged = 0x0008
    | LowerTextureUnpegged = 0x0010
    | Secret = 0x0020
    | BlocksSound = 0x0040
    | NerverShowsOnAutomap = 0x0080
    | AlwaysShowsOnAutomap = 0x0100

type Linedef = 
    {
        Start: Vector2
        End: Vector2
        FrontSidedef: Sidedef option
        BackSidedef: Sidedef option
        Flags: LinedefFlags
        SpecialType: int
        SectorTag: int
    }  
