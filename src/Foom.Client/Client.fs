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

type OptionBuilder() =
    member inline x.Bind(v,f) = Option.bind f v
    member inline x.Return v = Some v
    member inline x.ReturnFrom o = o
    member inline x.Zero () = None

let opt = OptionBuilder()

let create (app: Application) =
    ESystem.create "Client"
        (
            [
                // Initialize
                Behavior.update (fun _ entityManager eventManager ->
                    match entityManager.TryFind<WadComponent> (fun _ _ -> true) with
                    | None ->
                        let ent = entityManager.Spawn ()
                        entityManager.AddComponent ent (WadComponent("doom2.wad"))
                        entityManager.AddComponent ent (LevelComponent("map10"))

                    | _ -> ()
                )
            ]
            @
            Level.updates ()
            @
            [
                Camera.update (app)

                Behavior.eventQueue (fun (evt: Events.ComponentAdded<CharacterControllerComponent>) _ em ->
                    opt {
                        let! charContrComp = em.TryGet<CharacterControllerComponent> evt.Entity
                        let! transformComp = em.TryGet<TransformComponent> evt.Entity
                        let! (_, physicsEngineComp) = em.TryFind<PhysicsEngineComponent> (fun _ _ -> true)

                        physicsEngineComp.PhysicsEngine
                        |> PhysicsEngine.warpDynamicCircle transformComp.Position charContrComp.Circle

                    }
                    |> ignore
                )

                Behavior.update (fun _ em _ ->

                    em.TryFind<PhysicsEngineComponent, WireframeComponent> (fun _ _ _ -> true)
                    |> Option.iter (fun (_, physicsEngineComp, wireframeComp) ->

                        em.TryFind<CharacterControllerComponent, TransformComponent> (fun _ _ _ -> true)
                        |> Option.iter (fun (_, charContrComp, transformComp) ->
                            let pos = transformComp.Position
                            let pos = Vector2 (pos.X, pos.Y)

                            physicsEngineComp.PhysicsEngine
                            |> PhysicsEngine.findWithPoint pos
                            |> printfn "In Sector: %A"

                            physicsEngineComp.PhysicsEngine
                            |> PhysicsEngine.moveDynamicCircle transformComp.Position charContrComp.Circle

                            transformComp.Position <- Vector3 (charContrComp.Circle.Circle.Center, transformComp.Position.Z)

                            // *** TEMPORARY ***
                            wireframeComp.Position.Set [||]

                            //let boxes = ResizeArray ()
                            //physicsEngineComp.PhysicsEngine
                            //|> PhysicsEngine.debugFindSpacesByDynamicCircle charContrComp.Circle
                            //|> Seq.iter (fun b ->
                            //    let min = b.Min ()
                            //    let max = b.Max ()
                            //    [|
                            //        Vector3 (min.X, min.Y, 0.f)
                            //        Vector3 (max.X, min.Y, 0.f)
                
                            //        Vector3 (max.X, min.Y, 0.f)
                            //        Vector3 (max.X, max.Y, 0.f)
                
                            //        Vector3 (max.X, max.Y, 0.f)
                            //        Vector3 (min.X, max.Y, 0.f)
                
                            //        Vector3 (min.X, max.Y, 0.f)
                            //        Vector3 (min.X, min.Y, 0.f)
                            //    |]
                            //    |> boxes.AddRange
                            //)

                            //boxes
                            //|> Array.ofSeq
                            //|> wireframeComp.Position.Set

                            let tris = ResizeArray ()
                            let lines = ResizeArray ()
                            physicsEngineComp.PhysicsEngine
                            |> PhysicsEngine.iterWithPoint pos 
                                (fun tri ->
                                    tris.Add tri
                                )
                                (fun lined ->
                                    let t, d = lined.LineSegment |> LineSegment2D.findClosestPointByPoint pos
                                    lines.Add (Vector3 (lined.LineSegment.A, 0.f))
                                    lines.Add (Vector3 (lined.LineSegment.B, 0.f))
                                    lines.Add (Vector3 (d, 0.f))
                                    lines.Add (Vector3 (pos, 0.f))
                                )

                            let renderLines =
                                tris
                                |> Seq.map (fun tri -> 
                                    [|
                                    Vector3 (tri.A, 0.f);Vector3 (tri.B, 0.f)
                                    Vector3 (tri.B, 0.f);Vector3 (tri.C, 0.f)
                                    Vector3 (tri.C, 0.f);Vector3 (tri.A, 0.f)
                                    |]
                                )

                            if renderLines |> Seq.isEmpty |> not then
                                //renderLines
                                //|> Seq.reduce Array.append
                                //|> Array.append (lines |> Array.ofSeq)
                                (lines |> Array.ofSeq)
                                |> wireframeComp.Position.Set
                            // ******************
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