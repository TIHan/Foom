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

    interface IComponent

[<Sealed>]
type WadComponent =

    new : wadName: string -> WadComponent

    interface IComponent

module Behavior =

    val wadLoading : openWad: (string -> Stream) -> (Wad -> EntityManager -> unit) -> Behavior<_>

    val levelLoading : (Wad -> Level -> EntityManager -> unit) -> Behavior<_>