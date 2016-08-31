[<RequireQualifiedAccess>]
module Foom.Client.ClientSystem

open System
open System.IO
open System.Drawing
open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Physics
open Foom.Geometry
open Foom.DataStructures
open Foom.Renderer
open Foom.Wad
open Foom.Wad.Level
open Foom.Wad.Level.Structures
open Foom.Level.Components
open Foom.Common.Components

type ClientState = 
    {
        Window: nativeint
        Update: (float32 * float32 -> unit)
        RenderUpdate: (float32 * float32 -> unit)
    }

// These are sectors to look for and test to ensure things are working as they should.
// 568 - map10 sunder
// 4 - map10  sunder
// 4371 - map14 sunder
// 28 - e1m1 doom
// 933 - map10 sunder
// 20 - map10 sunder
// 151 - map10 sunder
// 439 - map08 sunder
// 271 - map03 sunder
// 663 - map05 sunder
// 506 - map04 sunder
// 3 - map02 sunder
// 3450 - map11 sunder
// 1558 - map11 sunder
// 240 - map07 sunder
// 2021 - map11 sunder

let create (app: Application) =
    ESystem.create "Client"
        (
            [
                // Initialize
                Behavior.update (fun _ entityManager eventManager ->
                    match entityManager.TryFind<WadComponent> (fun _ _ -> true) with
                    | None ->
                        let ent = entityManager.Spawn ()
                        entityManager.AddComponent ent (WadComponent("doom.wad"))
                        entityManager.AddComponent ent (LevelComponent("e1m3"))

                    | _ -> ()
                )
            ]
            @
            Level.updates ()
            @
            [
                Camera.update (app)

                Behavior.update (fun _ em _ ->

                    em.TryFind<Foom.Client.Level.SpatialComponent> (fun _ _ -> true)
                    |> Option.iter (fun (_, spatialComp) ->

                        em.TryFind<CameraComponent, TransformComponent> (fun _ _ _ -> true)
                        |> Option.iter (fun (_, _, transformComp) ->
                            let pos = transformComp.Position
                            let pos = Vector2 (pos.X, pos.Y)

                            spatialComp.SpatialHash
                            |> SpatialHash2D.queryWithPoint pos (fun sectorId ->
                                printfn "In Sector: %A" sectorId
                            )
                        )
                    )

                )
            ]
        )

let init (world: World) =
    let app = Renderer.init ()
    let sys1 = RendererSystem.create (app)
    let updateSys1 = world.AddESystem sys1

    { 
        Window = app.Window
        Update = create app |> world.AddESystem
        RenderUpdate = updateSys1
    }

let draw currentTime t (prev: ClientState) (curr: ClientState) =
    curr.RenderUpdate (currentTime, t)