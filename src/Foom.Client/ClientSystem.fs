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
open Foom.Wad.Components
open Foom.Common.Components

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

type LoadWadAndLevelRequested (wad, level) =

    member this.Wad = wad

    member this.Level = level

    interface IEvent

let loadWadAndLevelBehavior (clientWorld: ClientWorld) =
    Behavior.handleLatestEvent (fun (evt: LoadWadAndLevelRequested) _ em ->
        match em.TryGet<WadComponent> clientWorld.Entity with
        | None ->

            em.Add (clientWorld.Entity, WadComponent(evt.Wad))
            em.Add (clientWorld.Entity, LevelComponent(evt.Level))

        | Some _ ->

            clientWorld.DestroyEntities ()

            em.Remove<WadComponent> clientWorld.Entity
            em.Remove<LevelComponent> clientWorld.Entity

            em.Add (clientWorld.Entity, WadComponent(evt.Wad))
            em.Add (clientWorld.Entity, LevelComponent(evt.Level))
    )

let addRigidBodyBehavior (clientWorld: ClientWorld) =
    // This should be part of a physics system.
    Behavior.handleEvent (fun (evt: Events.ComponentAdded<CharacterControllerComponent>) _ em ->
        opt {
            let! charContrComp = em.TryGet<CharacterControllerComponent> evt.Entity
            let! transformComp = em.TryGet<TransformComponent> evt.Entity
            let! physicsEngineComp = em.TryGet<PhysicsEngineComponent> clientWorld.Entity

            physicsEngineComp.PhysicsEngine
            |> PhysicsEngine.addRigidBody charContrComp.RigidBody

        }
        |> ignore
    )

let physicsUpdateBehavior (clientWorld: ClientWorld) =
    Behavior.update (fun _ em _ ->

        opt {
            let! physicsEngineComp = em.TryGet<PhysicsEngineComponent> clientWorld.Entity
            //let! wireframeComp = em.TryGet<WireframeComponent> clientWorld.Entity
            let! (_, charContrComp, transformComp) = em.TryFind<CharacterControllerComponent, TransformComponent> (fun _ _ _ -> true)

            let pos = transformComp.Position
            let pos = Vector2 (pos.X, pos.Y)

            // TODO: This can be null, fix it.
            let sector =
                physicsEngineComp.PhysicsEngine
                |> PhysicsEngine.findWithPoint pos :?> Sector

            let rbody : RigidBody = charContrComp.RigidBody

            physicsEngineComp.PhysicsEngine
            |> PhysicsEngine.moveRigidBody transformComp.Position rbody

            transformComp.Position <- Vector3 (rbody.WorldPosition, single sector.FloorHeight + 50.f)

            // *** TEMPORARY ***
            //wireframeComp.Position.Set [||]

            //let boxes = ResizeArray ()
            //physicsEngineComp.PhysicsEngine
            //|> PhysicsEngine.debugFindSpacesByRigidBody charContrComp.RigidBody
            //|> Seq.iter (fun b ->
            //    let b = rbody.AABB
            //    let min = b.Min () + rbody.WorldPosition
            //    let max = b.Max () + rbody.WorldPosition
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

            //let tris = ResizeArray ()
            //let lines = ResizeArray ()
            //physicsEngineComp.PhysicsEngine
            //|> PhysicsEngine.iterWithPoint pos 
            //    (fun tri ->
            //        tris.Add tri
            //    )
            //    (fun seg ->
            //        let t, d = seg |> LineSegment2D.findClosestPointByPoint pos
            //        lines.Add (Vector3 (seg.A, 0.f))
            //        lines.Add (Vector3 (seg.B, 0.f))
            //        lines.Add (Vector3 (d, 0.f))
            //        lines.Add (Vector3 (pos, 0.f))
            //    )

            //let renderLines =
            //    tris
            //    |> Seq.map (fun tri -> 
            //        [|
            //        Vector3 (tri.A, 0.f);Vector3 (tri.B, 0.f)
            //        Vector3 (tri.B, 0.f);Vector3 (tri.C, 0.f)
            //        Vector3 (tri.C, 0.f);Vector3 (tri.A, 0.f)
            //        |]
            //    )

            //if renderLines |> Seq.isEmpty |> not then
            //    //renderLines
            //    //|> Seq.reduce Array.append
            //    //|> Array.append (lines |> Array.ofSeq)
            //    (lines |> Array.ofSeq)
            //    |> wireframeComp.Position.Set
            // ******************
        } |> ignore

    )


let create (app: Application) (clientWorld: ClientWorld) =
    ESystem.create "Client"
        (
            [
                loadWadAndLevelBehavior clientWorld
            ]
            @
            Level.updates clientWorld
            @
            [
                Camera.update (app)

                addRigidBodyBehavior clientWorld
               // physicsUpdateBehavior clientWorld
            ]
        )
