namespace Foom.Game.Wad

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
        Behavior.HandleComponentAdded (fun ent (wadComp: WadComponent) _ em ->
            match wadComp.State with
            | WadState.ReadyToLoad fileName ->

                let wad = Wad.create (openWad fileName)
                //let wad = Wad.extend (openWad "Untitled.wad") wad
               // let wad = Wad.extend (openWad "sunder.wad") wad
                wadComp.State <- WadState.Loaded wad
                f wad em

            | _ -> ()
        )

    let levelLoading f =
        Behavior.HandleComponentAdded (fun ent (levelComp: LevelComponent) _ em ->
            match levelComp.State with
            | LevelState.ReadyToLoad levelName ->

                match em.TryGet<WadComponent> ent with
                | Some wadComp ->

                    match wadComp.State with
                    | WadState.Loaded wad ->
                  
                        let level = Wad.findLevel levelName wad
                        levelComp.State <- LevelState.Loaded level
                        f wad level em

                    | WadState.ReadyToLoad wadName ->

                        Debug.WriteLine (
                            String.Format ("Tried to load level, {0}, but WAD, {1}, is not loaded.", levelName, wadName)
                        )

                | _ ->

                    Debug.WriteLine (
                        String.Format ("Tried to load level, {0}, but no WAD found.", levelName)
                    )

            | _ -> ()
        )
