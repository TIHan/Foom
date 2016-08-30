namespace Foom.Level.Components // TODO: Rename to Foom.Wad.Components

open System
open System.IO
open System.Numerics
open System.Diagnostics

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level

[<RequireQualifiedAccess>]
type LevelState =
    | ReadyToLoad of string
    | Loaded of Level
  
[<Sealed>]
type LevelComponent (levelName: string) =

    member val State = LevelState.ReadyToLoad levelName with get, set

    interface IComponent

[<RequireQualifiedAccess>]
type WadState =
    | ReadyToLoad of string
    | Loaded of Wad

[<Sealed>]
type WadComponent (wadName: string) =

    member val State = WadState.ReadyToLoad wadName with get, set

    interface IComponent

module Sys =

    let wadLoading (openWad: string -> Stream) f =
        eventQueue (fun (evt: Events.ComponentAdded<WadComponent>) _ entityManager ->

            entityManager.TryGet<WadComponent> evt.Entity
            |> Option.iter (fun wadComp ->

                match wadComp.State with
                | WadState.ReadyToLoad fileName ->

                    let wad = Wad.create (openWad fileName)
                    wadComp.State <- WadState.Loaded wad
                    f wad entityManager

                | _ -> ()
            )
        )

    let levelLoading f =
        eventQueue (fun (evt: Events.ComponentAdded<LevelComponent>) _ entityManager ->

            entityManager.TryGet<LevelComponent> evt.Entity
            |> Option.iter (fun levelComp ->

                match levelComp.State with
                | LevelState.ReadyToLoad levelName ->

                    match entityManager.TryGet<WadComponent> evt.Entity with
                    | Some wadComp ->

                        match wadComp.State with
                        | WadState.Loaded wad ->
                      
                            let level = Wad.findLevel levelName wad
                            levelComp.State <- LevelState.Loaded level
                            f wad level entityManager

                        | WadState.ReadyToLoad wadName ->

                            Debug.WriteLine (
                                String.Format ("Tried to load level, {0}, but WAD, {1}, is not loaded.", levelName, wadName)
                            )
                            entityManager.RemoveComponent<LevelComponent> evt.Entity

                    | _ ->

                        Debug.WriteLine (
                            String.Format ("Tried to load level, {0}, but no WAD found.", levelName)
                        )
                        entityManager.RemoveComponent<LevelComponent> evt.Entity

                | _ -> ()
            )
        )
