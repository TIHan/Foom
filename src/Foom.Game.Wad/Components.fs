namespace Foom.Game.Wad

open System
open System.IO
open System.Numerics
open System.Diagnostics
open System.Collections.Generic

open Foom.Ecs
open Foom.Wad
open Foom.Wad.Level
  
[<Sealed>]
type LevelComponent (levelName: string) =
    inherit Component ()

    member __.LevelName = levelName

[<Sealed>]
type WadComponent (wadName: string) =
    inherit Component ()

    member __.WadName = wadName

module Behavior =

    let wadLevelLoading (openWad: string -> Stream) (f : Wad -> unit) (g : Wad -> Level -> EntityManager -> unit) : Behavior<_> =
        let levelQueue = Queue ()
        let mutable levelOpt = None
        let mutable wadOpt = None


        let added =
            Behavior.ComponentAdded (fun _ _ (wadComp: WadComponent) ->
                let fileName = wadComp.WadName
                let wad = Wad.create (openWad fileName)
                //let wad = Wad.extend (openWad "Untitled.wad") wad
                // let wad = Wad.extend (openWad "sunder.wad") wad
                wadOpt <- Some wad
                f wad
            )


        Behavior.Merge 
            [
                added

                Behavior.ComponentAdded (fun _ _ (levelComp: LevelComponent) (wadComp : WadComponent) ->
                    let levelName = levelComp.LevelName
                    levelQueue.Enqueue levelName
                )

                Behavior.Update (fun _ em _ ->
                    while levelQueue.Count > 0 do
                        let levelName = levelQueue.Dequeue ()
                        match wadOpt with
                        | Some wad ->
                            let level = Wad.findLevel levelName wad
                            levelOpt <- Some level
                            g wad level em
                        | _ ->
                            Debug.WriteLine (
                                String.Format ("Tried to load level, {0}, but a WAD is not loaded.", levelName)
                            )
                )
            ]