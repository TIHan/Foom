namespace Foom.Level.Components

open System
open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level

module Request =

    val loadLevel : name: string -> EventManager -> unit

    val loadWad : fileName: string -> EventManager -> unit

module Sys =

    val handleLoadWadRequests : openWad: (string -> Stream) -> Sys<_>

    val handleWadLoaded : (Wad -> EntityManager -> unit) -> Sys<_>

    val handleLoadLevelRequests : (Wad -> Level -> EntityManager -> unit) -> Sys<_>