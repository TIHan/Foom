namespace Foom.Wad.Pickler

open System
open System.Numerics
open Foom.Pickler.Core
open Foom.Pickler.Unpickle

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

[<Flags>]
type LinedefDataFlags =
    | BlocksPlayersAndMonsters = 0x0001
    | BlocksMonsters = 0x0002
    | TwoSided = 0x0004
    | UpperTextureUnpegged = 0x0008
    | LowerTextureUnpegged = 0x0010
    | Secret = 0x0020
    | BlocksSound = 0x0040
    | NerverShowsOnAutomap = 0x0080
    | AlwaysShowsOnAutomap = 0x0100

type SidedefData = {
    OffsetX: int
    OffsetY: int
    UpperTextureName: string
    LowerTextureName: string
    MiddleTextureName: string
    SectorNumber: int }

type DoomLinedefData = { 
    Flags: LinedefDataFlags
    SpecialType: int
    SectorTag: int }

type LinedefData =
    | Doom of x: Vector2 * y: Vector2 * front: SidedefData option * back: SidedefData option * DoomLinedefData

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
    Linedefs: LinedefData [] }

type LumpThings = { Things: ThingData [] }
type LumpLinedefs = { Linedefs: LinedefData [] }
type LumpSidedefs = { Sidedefs: SidedefData [] }
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

type Texture =
    {
        Name: string
    }

module UnpickleWad =

    val u_lumpHeader : Unpickle<LumpHeader>

    val u_lumpHeaders : count: int -> offset: int64 -> Unpickle<LumpHeader []>

    val u_wad : Unpickle<WadData>

    val u_lumpThings : format: ThingDataFormat -> size: int -> offset: int64 -> Unpickle<LumpThings>

    val u_lumpVertices : size: int -> offset: int64 -> Unpickle<LumpVertices>

    val u_lumpSidedefs : size: int -> offset: int64 -> Unpickle<LumpSidedefs>

    val u_lumpLinedefs : vertices: Vector2 [] -> sidedefs: SidedefData [] -> size: int -> offset: int64 -> Unpickle<LumpLinedefs>

    val u_lumpSectors : linedefs: LinedefData [] -> size: int -> offset: int64 -> Unpickle<LumpSectors>

    val u_lumpPalettes : size: int -> offset: int64 -> Unpickle<PaletteData []>

    val u_lumpRaw : size: int -> offset: int64 -> Unpickle<byte []>

    val uTextureHeader : LumpHeader -> Unpickle<TextureHeader>

    val uTextures : LumpHeader -> TextureHeader -> Unpickle<Texture []>

    val uPatchNames : LumpHeader -> Unpickle<string []>