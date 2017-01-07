namespace Foom.Wad

open System

type ThingType =
    | Player1Start = 1
    | Player2Start = 2
    | Player3Start = 3
    | Player4Start = 4

[<Flags>]
type DoomThingFlags =
    | SkillLevelOneAndTwo = 0x001
    | SkillLevelThree = 0x002
    | SkillLevelFourAndFive = 0x004
    | Deaf = 0x008
    | NotInSinglePlayer = 0x0010
    | NotInDeathmatch = 0x0020 // boom
    | NotInCoop = 0x0040 // boom
    | FriendlyMonster = 0x0080 // MBF

[<Flags>]
type HexenThingFlags =
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
 
type DoomThing = { X: int; Y: int; Angle: int; Type: ThingType; Flags: DoomThingFlags }

type HexenThing = { Id: int; X: int; Y: int; StartingHeight: int; Angle: int; Flags: HexenThingFlags; Arg1: byte; Arg2: byte; Arg3: byte; Arg4: byte; Arg5: byte }

type Thing =
    | Doom of DoomThing
    | Hexen of HexenThing
