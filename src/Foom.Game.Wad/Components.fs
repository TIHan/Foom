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

    [<Sealed>]
    type Context (openWad: string -> Stream, f : Wad -> unit, g : Wad -> Level -> EntityManager -> unit) =

        member __.OpenWad = openWad

        member __.OnWadLoaded = f

        member __.OnLevelLoaded = g

    let wadLevelLoading : Behavior<Context> =
        Behavior.Delay <| fun () ->
            let levelQueue = Queue ()
            let mutable levelOpt = None
            let mutable wadOpt = None


            let added =
                Behavior.contramap (fun (context : Context) -> (context.OpenWad, context.OnWadLoaded))
                <| Behavior.ComponentAdded (fun (openWad, onWadLoaded : Wad -> unit) _ (wadComp: WadComponent) ->
                    let fileName = wadComp.WadName
                    let wad = Wad.create (openWad fileName)
                    //let wad = Wad.extend (openWad "Untitled.wad") wad
                    // let wad = Wad.extend (openWad "sunder.wad") wad
                    wadOpt <- Some wad
                    onWadLoaded wad
                )
    
            Behavior.Merge 
                [
                    added

                    Behavior.ComponentAdded (fun _ _ (levelComp: LevelComponent) (wadComp : WadComponent) ->
                        let levelName = levelComp.LevelName
                        levelQueue.Enqueue levelName
                    )

                    Behavior.contramap (fun (context : Context) -> context.OnLevelLoaded)
                    <| Behavior.Update (fun onLevelLoaded em _ ->
                        while levelQueue.Count > 0 do
                            let levelName = levelQueue.Dequeue ()
                            match wadOpt with
                            | Some wad ->
                                let level = Wad.findLevel levelName wad
                                levelOpt <- Some level
                                onLevelLoaded wad level em
                            | _ ->
                                Debug.WriteLine (
                                    String.Format ("Tried to load level, {0}, but a WAD is not loaded.", levelName)
                                )
                    )
                ]