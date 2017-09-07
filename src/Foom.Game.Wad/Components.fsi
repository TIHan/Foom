namespace Foom.Game.Wad

open System
open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level
  
[<Sealed>]
type LevelComponent =
    inherit Component

    new : levelName: string -> LevelComponent

    member LevelName : string

[<Sealed>]
type WadComponent =
    inherit Component

    new : wadName: string -> WadComponent

    member WadName : string

module Behavior =

    val wadLevelLoading : openWad: (string -> Stream) -> (Wad -> unit) -> (Wad -> Level -> EntityManager -> unit) -> Behavior<unit>