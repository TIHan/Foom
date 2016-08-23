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

[<Sealed>]
type LevelComponent (level: Level) =

    member this.Level = level

    interface IEntityComponent

[<Sealed>]
type WadComponent (wad: Wad) =

    member this.Wad = wad

    interface IEntityComponent

module Request =

    let loadLevel name (eventManager: EventManager) =
        eventManager.Publish (LoadLevelRequested name)

    let loadWad fileName (eventManager: EventManager) =
        eventManager.Publish (LoadWadRequested fileName)

module Sys =

    let handleLoadWadRequests (openWad: string -> Stream) =
        eventQueue (fun (evt: LoadWadRequested) _ entityManager ->
            let wad = Wad.create (openWad (evt.Name))
            let ent = entityManager.Spawn ()

            entityManager.AddComponent ent (WadComponent (wad))
        )

    let handleWadLoaded f =
        eventQueue (fun (evt: Events.ComponentAdded<WadComponent>) _ entityManager ->
            entityManager.TryGet<WadComponent> (evt.Entity)
            |> Option.iter (fun wadComp ->
                f wadComp.Wad entityManager
            )
        )

    let handleLoadLevelRequests f =
        eventQueue (fun (evt: LoadLevelRequested) _ entityManager ->
            match entityManager.TryFind<WadComponent> (fun _ _ -> true) with
            | Some (ent, wadComp) ->
                let level = Wad.findLevel evt.Name wadComp.Wad
                entityManager.AddComponent ent (LevelComponent (level))
                f wadComp.Wad level entityManager
            | _ -> ()
        )