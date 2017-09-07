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

    [<Sealed>]
    type Context =

        new : openWad : (string -> Stream) * f : (Wad -> unit) * g : (Wad -> Level -> EntityManager -> unit) -> Context

    val wadLevelLoading : Behavior<Context>