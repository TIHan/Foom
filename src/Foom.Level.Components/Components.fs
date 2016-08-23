namespace Foom.Level.Components

open System
open System.IO
open System.Numerics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level

type SectorGeometry =
    | Static of Flat seq * Wall seq * lightLevel: byte

type LoadLevelRequested (name: string) =

    let calculateStaticGeometry f (level: Level) =
        lazy
            level.Sectors
            |> Seq.iteri (fun i sector ->
                let flats = Level.createFlats i level
                let walls = Level.createWalls i level
                Static (flats, walls, Level.lightLevelBySectorId i level)
                |> f i
            )

    member this.StaticGeometry f (level: Level) = (calculateStaticGeometry f level).Force()

    member this.Name = name

    interface IEntitySystemEvent

type WadComponent (wad: Wad) =

    member this.Wad = wad

    interface IEntityComponent

type LoadWadRequested (name: string) =

    member this.Name = name

    interface IEntitySystemEvent

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
                f entityManager wadComp.Wad
            )
        )

    let handleLoadLevelRequests f =
        eventQueue (fun entityManager _ (evt: LoadLevelRequested) ->
            match entityManager.TryFind<WadComponent> (fun _ _ -> true) with
            | Some (_, wadComp) ->
                let level = Wad.findLevel evt.Name wadComp.Wad
                evt.StaticGeometry (fun sectorId geos -> f entityManager wadComp.Wad sectorId geos) level
            | _ -> ()
        )