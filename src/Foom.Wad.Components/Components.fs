namespace Foom.Wad.Components

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
    inherit Component ()

    member val State = LevelState.ReadyToLoad levelName with get, set

[<RequireQualifiedAccess>]
type WadState =
    | ReadyToLoad of string
    | Loaded of Wad

[<Sealed>]
type WadComponent (wadName: string) =
    inherit Component ()

    member val State = WadState.ReadyToLoad wadName with get, set

module Behavior =

    let wadLoading (openWad: string -> Stream) f =
        Behavior.handleEvent (fun (evt: Events.ComponentAdded<WadComponent>) _ entityManager ->

            entityManager.TryGet<WadComponent> evt.Entity
            |> Option.iter (fun wadComp ->

                match wadComp.State with
                | WadState.ReadyToLoad fileName ->

                    let wad = Wad.create (openWad fileName)
                    //let wad = Wad.extend (openWad "Untitled.wad") wad
                   // let wad = Wad.extend (openWad "sunder.wad") wad
                    wadComp.State <- WadState.Loaded wad
                    f wad entityManager

                | _ -> ()
            )
        )

    let levelLoading f =
        Behavior.handleEvent (fun (evt: Events.ComponentAdded<LevelComponent>) _ entityManager ->

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
                            entityManager.Remove<LevelComponent> evt.Entity

                    | _ ->

                        Debug.WriteLine (
                            String.Format ("Tried to load level, {0}, but no WAD found.", levelName)
                        )
                        entityManager.Remove<LevelComponent> evt.Entity

                | _ -> ()
            )
        )
