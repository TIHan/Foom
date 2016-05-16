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

    new (r, g, b) = { R = r; G = g; B = b }

type PaletteData = { Pixels: Pixel [] }

module UnpickleWad =

    let inline u_arrayi n (p: int -> Unpickle<'a>) =
        fun stream ->
            match n with
            | 0 -> [||]
            | _ -> Array.init n (fun i -> p i stream)

    let inline fixedToSingle x = (single x / 65536.f)

    let u_header : Unpickle<Header> =
        u_pipe3 (u_string 4) u_int32 u_int32 <|
        fun id lumpCount lumpOffset ->
            { IsPwad = if id = "IWAD" then false else true
              LumpCount = lumpCount
              LumpOffset = lumpOffset }

    let u_lumpHeader : Unpickle<LumpHeader> =
        u_pipe3 u_int32 u_int32 (u_string 8) <| fun offset size name -> { Offset = offset; Size = size; Name = name.Trim().Trim('\000') }

    let u_lumpHeaders count offset : Unpickle<LumpHeader []> =
        u_skipBytes offset >>. u_array count u_lumpHeader

    let filterLumpHeaders (lumpHeaders: LumpHeader []) =
        lumpHeaders
        |> Array.filter (fun x ->
            match x.Name.ToUpper () with
            | "F1_START" -> false
            | "F2_START" -> false
            | "F3_START" -> false
            | "F1_END" -> false
            | "F2_END" -> false
            | "F3_END" -> false
            | _ -> true)

    let u_wad : Unpickle<WadData> =
        u_lookAhead u_header >>= fun header ->
            (u_lookAhead <| (u_lumpHeaders header.LumpCount (int64 header.LumpOffset)) |>> (fun lumpHeaders -> { Header = header; LumpHeaders = filterLumpHeaders lumpHeaders }))

    [<Literal>]
    let doomThingSize = 10
    [<Literal>]
    let hexenThingSize = 20
    let u_thing format : Unpickle<ThingData> =
        match format with
        | ThingDataFormat.Doom ->
            u_pipe5 u_int16 u_int16 u_int16 u_int16 u_int16 <|
            fun x y angle _ flags ->
                ThingData.Doom { X = int x; Y = int y; Angle = int angle; Flags = enum<DoomThingDataFlags> (int flags) }
        | _ -> failwith "Not supported."

    let u_things format count offset : Unpickle<ThingData []> =
        u_skipBytes offset >>. u_array count (u_thing format)

    let u_lumpThings format size offset : Unpickle<LumpThings> =
        match format with
        | ThingDataFormat.Doom ->
            u_lookAhead (u_things format (size / doomThingSize) offset) |>> fun things -> { Things = things }
        | _ -> failwith "Not supported."

    [<Literal>]
    let vertexSize = 4
    let u_vertex : Unpickle<Vector2> =
        u_pipe2 u_int16 u_int16 <|
        fun x y -> Vector2 (fixedToSingle x, fixedToSingle y)

    let u_vertices count offset : Unpickle<Vector2 []> =
        u_skipBytes offset >>. u_array count u_vertex

    let u_lumpVertices size offset : Unpickle<LumpVertices> =
        u_lookAhead (u_vertices (size / vertexSize) offset) |>> fun vertices -> { Vertices = vertices }

    [<Literal>]
    let sidedefSize = 30
    let u_sidedef : Unpickle<SidedefData> =
        u_pipe6 u_int16 u_int16 (u_string 8) (u_string 8) (u_string 8) u_int16 <|
        fun offsetX offsetY upperTexName lowerTexName middleTexName sectorNumber ->
            { OffsetX = int offsetX
              OffsetY = int offsetY
              UpperTextureName = upperTexName.Trim().Trim('\000')
              LowerTextureName = lowerTexName.Trim().Trim('\000')
              MiddleTextureName = middleTexName.Trim().Trim('\000')
              SectorNumber = int sectorNumber }

    let u_sidedefs count offset : Unpickle<SidedefData []> =
        u_skipBytes offset >>. u_array count u_sidedef

    let u_lumpSidedefs size offset : Unpickle<LumpSidedefs> =
        u_lookAhead (u_sidedefs (size / sidedefSize) offset) |>> fun sidedefs -> { Sidedefs = sidedefs }

    [<Literal>]
    let linedefSize = 14
    let u_linedef (vertices: Vector2 []) (sidedefs: SidedefData []) : Unpickle<LinedefData> =
        u_pipe7 u_uint16 u_uint16 u_int16 u_int16 u_int16 u_uint16 u_uint16 <|
        fun startVertex endVertex flags specialType sectorTag rightSidedef leftSidedef ->
            let data =
                { Flags = enum<LinedefDataFlags> (int flags)
                  SpecialType = int specialType
                  SectorTag = int sectorTag }
            let f = match int rightSidedef with | n when n <> 65535 -> Some sidedefs.[n] | _ -> None
            let b = match int leftSidedef with | n when n <> 65535 -> Some sidedefs.[n] | _ -> None
            LinedefData.Doom (
                vertices.[int startVertex],
                vertices.[int endVertex],
                f,
                b,
                data)

    let u_linedefs (vertices: Vector2 []) (sidedefs: SidedefData []) count offset : Unpickle<LinedefData []> =
        u_skipBytes offset >>. u_array count (u_linedef vertices sidedefs)
        
    let u_lumpLinedefs (vertices: Vector2 []) (sidedefs: SidedefData []) size offset : Unpickle<LumpLinedefs> =
        u_lookAhead (u_linedefs vertices sidedefs (size / linedefSize) offset) |>> fun linedefs -> { Linedefs = linedefs }

    [<Literal>]
    let sectorSize = 26
    let u_sector (linedefs: LinedefData []) (i: int) : Unpickle<SectorData> =
        u_pipe7 u_int16 u_int16 (u_string 8) (u_string 8) u_int16 u_int16 u_int16 <|
        fun floorHeight ceilingHeight floorTexName ceilingTexName lightLevel typ tag ->
            { FloorHeight = int floorHeight
              CeilingHeight = int ceilingHeight
              FloorTextureName = floorTexName.Trim().Trim('\000')
              CeilingTextureName = ceilingTexName.Trim().Trim('\000')
              LightLevel = int lightLevel
              Type = enum<SectorDataType> (int typ)
              Tag = int tag
              Linedefs = 
                linedefs
                |> Array.filter (function
                    | LinedefData.Doom (_, _, f, b, _) -> 
                        match f, b with
                        | Some f, Some b -> f.SectorNumber = i || b.SectorNumber = i
                        | Some f, _ -> f.SectorNumber = i
                        | _, Some b -> b.SectorNumber = i
                        | _ -> false) }

    let u_sectors (linedefs: LinedefData []) count offset : Unpickle<SectorData []> =
        u_skipBytes offset >>. u_arrayi count (u_sector linedefs)

    let u_lumpSectors (linedefs: LinedefData []) size offset : Unpickle<LumpSectors> =
        u_lookAhead (u_sectors linedefs (size / sectorSize) offset) |>> fun sectors -> { Sectors = sectors }

    [<Literal>]
    let flatSize = 4096

    [<Literal>]
    let paletteSize = 768
    let u_pixel =
        u_pipe3 u_byte u_byte u_byte <| fun r g b -> Pixel (r, g, b)

    let u_pixels count = u_array count u_pixel

    let u_palette = (u_pixels (paletteSize / sizeof<Pixel>)) |>> fun pixels -> { Pixels = pixels }

    let u_palettes count offset : Unpickle<PaletteData []> =
        u_skipBytes offset >>. u_array count (u_palette)

    let u_lumpPalettes size offset : Unpickle<PaletteData []> =
        u_lookAhead (u_palettes (size / paletteSize) offset)

    let u_lumpRaw size offset : Unpickle<byte []> =
        u_lookAhead (u_skipBytes offset >>. u_bytes size)