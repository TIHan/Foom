namespace Foom.Wad

open System.Numerics

open Foom.Geometry

type SectorType =
    | Normal = 0
    | BlinkLightRandom = 1
    | BlinkLightHalfASecond = 2
    | BlinkLightdOneSecond = 3
    | TwentyPercentDamagePerSecondPlusBlinkLightHalfASecond = 4
    | TenPercentDamagePerSecond = 5
    | FivePercentDamagePerSecond = 7
    | LightOscillates = 8
    | PlayerEnteringSectorGetsCreditForFindingASecret = 9
    | ThirtySecondsAfterLevelStartCeilingClosesLikeADoor = 10
    | CancelGodModeAndTwentyPercentDamagePerSecondAndWhenPlayerDiesLevelEnds = 11
    | BlinkLightHalfASecondSync = 12
    | BlinkLightOneSecondSync = 13
    | ThreeHundredSecondsAfterLevelStartCeilingOpensLikeADoor = 14
    | TwentyPercentDamagePerSecond = 16
    | FlickerLightRandomly = 17

[<NoComparison; ReferenceEquality>]
type Sector = 
    {
        Id: int
        FloorTextureName: string
        FloorHeight: int
        CeilingTextureName: string
        CeilingHeight: int
        LightLevel: int
        Type: SectorType
        Tag: int
    } 
