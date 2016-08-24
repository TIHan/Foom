namespace Foom.Level.Components

open System
open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level
  
[<Sealed>]
type LevelComponent =

    new : levelName: string -> LevelComponent

    interface IEntityComponent

[<Sealed>]
type WadComponent =

    new : wadName: string -> WadComponent

    interface IEntityComponent

module Sys =

    val wadLoading : openWad: (string -> Stream) -> (Wad -> EntityManager -> unit) -> Sys<_>

    val levelLoading : (Wad -> Level -> EntityManager -> unit) -> Sys<_>