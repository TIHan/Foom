namespace Foom.Wad.Components

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

[<Sealed>]
type WadComponent =
    inherit Component

    new : wadName: string -> WadComponent

module Behavior =

    val wadLoading : openWad: (string -> Stream) -> (Wad -> EntityManager -> unit) -> Behavior<_>

    val levelLoading : (Wad -> Level -> EntityManager -> unit) -> Behavior<_>