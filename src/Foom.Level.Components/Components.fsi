namespace Foom.Level.Components

open System
open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level

type SectorGeometry =
    | Static of Flat seq * Wall seq * lightLevel: byte

type LoadLevelRequested =

    new : name: string -> LoadLevelRequested

    member Name : string

    interface IEntitySystemEvent

type LoadWadRequested =

    new : name: string -> LoadWadRequested

    member Name : string

    interface IEntitySystemEvent

module Sys =

    val handleLoadWadRequests : openWad: (string -> Stream) -> Sys<_>

    val handleWadLoaded : (EntityManager -> Wad -> unit) -> Sys<_>

    val handleLoadLevelRequests : (EntityManager -> Wad -> int -> SectorGeometry -> unit) -> Sys<_>