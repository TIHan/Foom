namespace Foom.Level.Components

open System
open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level

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

    val handleWadLoaded : (Wad -> EntityManager -> unit) -> Sys<_>

    val handleLoadLevelRequests : (Wad -> Level -> EntityManager -> unit) -> Sys<_>