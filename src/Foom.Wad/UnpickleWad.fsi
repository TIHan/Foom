namespace Foom.Wad.Pickler

open System
open System.Numerics
open Foom.Pickler.Core
open Foom.Pickler.Unpickle
open Foom.Wad.Level.Structures

type Header = { IsPwad: bool; LumpCount: int; LumpOffset: int }
 
type LumpHeader = { Offset: int32; Size: int32; Name: string }
 
type WadData = { Header: Header; LumpHeaders: LumpHeader [] }

type ThingDataFormat =
    | Doom = 0
    | Hexen = 1

[<Flags>]
type DoomThingDataFlags =
    | SkillLevelOneAndTwo = 0x001
    | SkillLevelThree = 0x002
    | SkillLevelFourAndFive = 0x004
    | Deaf = 0x008
    | NotInSinglePlayer = 0x0010
//    | NotInDeathmatch = 0x0020 // boom
//    | NotInCoop = 0x0040 // boom
//    | FriendlyMonster = 0x0080 // MBF

[<Flags>]
type HexenThingDataFlags =
    | SkillLevelOneAndTwo = 0x001
    | SkillLevelThree = 0x002
    | SkillLevelFourAndFive = 0x004
    | Deaf = 0x008
    | Dormant = 0x0010
    | AppearOnlyToFighterClass = 0x0020
    | AppearOnlyToClericClass = 0x0040
    | AppearOnlyToMageClass = 0x0080
    | AppearOnlyInSinglePlayer = 0x0100
    | AppearOnlyInCoop = 0x0200
    | AppearOnlyInDeathmatch = 0x0400
 
type DoomThingData = { X: int; Y: int; Angle: int; Flags: DoomThingDataFlags }

type HexenThingData = { Id: int; X: int; Y: int; StartingHeight: int; Angle: int; Flags: HexenThingDataFlags; Arg1: byte; Arg2: byte; Arg3: byte; Arg4: byte; Arg5: byte }

type ThingData =
    | Doom of DoomThingData
    | Hexen of HexenThingData

type SectorDataType =
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

type SectorData = {
    FloorHeight: int
    CeilingHeight: int
    FloorTextureName: string
    CeilingTextureName: string
    LightLevel: int
    Type: SectorDataType;
    Tag: int
    Linedefs: Linedef [] }

type LumpThings = { Things: ThingData [] }
type LumpLinedefs = { Linedefs: Linedef [] }
type LumpSidedefs = { Sidedefs: Sidedef [] }
type LumpVertices = { Vertices: Vector2 [] }
type LumpSectors = { Sectors: SectorData [] }

[<Struct>]
type Pixel =
    val R : byte
    val G : byte
    val B : byte

    new : byte * byte * byte -> Pixel

type PaletteData = { Pixels: Pixel [] }

type TextureHeader =
    {
        Count: int
        Offsets: int []
    }

type TexturePatch =
    {
        OriginX: int
        OriginY: int
        PatchNumber: int
    }

type TextureInfo =
    {
        Name: string
        IsMasked: bool
        Width: int
        Height: int
        Patches: TexturePatch []
    }

type DoomPicture =
    {
        Width: int
        Height: int
        Top: int
        Left: int
        Data: Pixel [,]
    }

module UnpickleWad =

    val u_lumpHeader : Unpickle<LumpHeader>

    val u_lumpHeaders : count: int -> offset: int64 -> Unpickle<LumpHeader []>

    val u_wad : Unpickle<WadData>

    val u_lumpThings : format: ThingDataFormat -> size: int -> offset: int64 -> Unpickle<LumpThings>

    val u_lumpVertices : size: int -> offset: int64 -> Unpickle<LumpVertices>

    val u_lumpSidedefs : size: int -> offset: int64 -> Unpickle<LumpSidedefs>

    val u_lumpLinedefs : vertices: Vector2 [] -> sidedefs: Sidedef [] -> size: int -> offset: int64 -> Unpickle<LumpLinedefs>

    val u_lumpSectors : linedefs: Linedef [] -> size: int -> offset: int64 -> Unpickle<LumpSectors>

    val u_lumpPalettes : size: int -> offset: int64 -> Unpickle<PaletteData []>

    val u_lumpRaw : size: int -> offset: int64 -> Unpickle<byte []>

    val uTextureHeader : LumpHeader -> Unpickle<TextureHeader>

    val uTextureInfos : LumpHeader -> TextureHeader -> Unpickle<TextureInfo []>

    val uPatchNames : LumpHeader -> Unpickle<string []>

    val uDoomPicture : LumpHeader -> PaletteData -> Unpickle<DoomPicture>