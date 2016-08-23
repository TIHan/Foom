namespace Foom.Level.Components

open System
open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level

type LoadLevelRequested (name: string) =

    member this.Name = name

    interface IEntitySystemEvent

type LoadWadRequested (name: string) =

    member this.Name = name

    interface IEntitySystemEvent

type LevelComponent (level: Level) =

    member this.Level = level

    interface IEntityComponent

type WadComponent (wad: Wad) =

    member this.Wad = wad

    interface IEntityComponent

module Sys =

    let handleLoadWadRequests (openWad: string -> Stream) =
        eventQueue (fun entityManager _ (evt: LoadWadRequested) ->
            let wad = Wad.create (openWad (evt.Name))
            let ent = entityManager.Spawn ()

            entityManager.AddComponent ent (WadComponent (wad))
        )

    let handleWadLoaded f =
        eventQueue (fun entityManager _ (evt: Events.ComponentAdded<WadComponent>) ->
            entityManager.TryGet<WadComponent> (evt.Entity)
            |> Option.iter (fun wadComp ->
                f wadComp.Wad entityManager
            )
        )

    let handleLoadLevelRequests f =
        eventQueue (fun entityManager _ (evt: LoadLevelRequested) ->
            match entityManager.TryFind<WadComponent> (fun _ _ -> true) with
            | Some (_, wadComp) ->
                let level = Wad.findLevel evt.Name wadComp.Wad
                f wadComp.Wad level entityManager
            | _ -> ()
        )