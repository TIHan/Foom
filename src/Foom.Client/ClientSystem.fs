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
open Foom.Wad.Components

open Foom.Game.Core
open Foom.Game.Sprite

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

let addRigidBodyBehavior (clientWorld: ClientWorld) : Behavior<float32 * float32> =
    Behavior.merge
        [
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

            // This should be part of a physics system.
            Behavior.handleEvent (fun (evt: Events.ComponentAdded<RigidBodyComponent>) _ em ->
                opt {
                    let! rbodyComp = em.TryGet<RigidBodyComponent> evt.Entity
                    let! transformComp = em.TryGet<TransformComponent> evt.Entity
                    let! physicsEngineComp = em.TryGet<PhysicsEngineComponent> clientWorld.Entity

                    physicsEngineComp.PhysicsEngine
                    |> PhysicsEngine.addRigidBody rbodyComp.RigidBody
                }
                |> ignore
            )
    ]

let physicsSpriteBehavior (clientWorld: ClientWorld) =
    Behavior.handleComponentAdded (fun (ent: Entity) (spriteComp: SpriteComponent) _ em ->
        opt {
            let! charContrComp = em.TryGet<RigidBodyComponent> ent
            let! transformComp = em.TryGet<TransformComponent> ent
            let! physicsEngineComp = em.TryGet<PhysicsEngineComponent> clientWorld.Entity

            // TODO: This can be null, fix it.
            let sector =
                physicsEngineComp.PhysicsEngine
                |> PhysicsEngine.findWithPoint charContrComp.RigidBody.WorldPosition

            if obj.ReferenceEquals (sector, null) |> not then
                let sector = sector :?> Foom.Level.Sector

                spriteComp.LightLevel <- sector.lightLevel
                transformComp.Position <- Vector3 (transformComp.Position.X, transformComp.Position.Y, single sector.floorHeight)
        } |> ignore
    )

let physicsUpdateBehavior (clientWorld: ClientWorld) =
    Behavior.update (fun _ em _ ->

        opt {
            let! physicsEngineComp = em.TryGet<PhysicsEngineComponent> clientWorld.Entity
            //let! wireframeComp = em.TryGet<WireframeComponent> clientWorld.Entity
            let! (_, charContrComp, transformComp) = em.TryFind<CharacterControllerComponent, TransformComponent> (fun _ _ _ -> true)

            let pos = transformComp.Position
            let pos = Vector2 (pos.X, pos.Y)

            let rbody : RigidBody = charContrComp.RigidBody
            physicsEngineComp.PhysicsEngine
            |> PhysicsEngine.moveRigidBody transformComp.Position rbody

            transformComp.Position <- Vector3 (rbody.WorldPosition, transformComp.Position.Z)

            // TODO: This can be null, fix it.
            let sector =
                physicsEngineComp.PhysicsEngine
                |> PhysicsEngine.findWithPoint rbody.WorldPosition

            if obj.ReferenceEquals (sector, null) |> not then
                let sector = sector :?> Foom.Level.Sector

                transformComp.Position <- Vector3 (rbody.WorldPosition, single sector.floorHeight + 50.f)
        } |> ignore

    )


let create (app: Application) (clientWorld: ClientWorld) am =
    Behavior.merge
        (
            [
                loadWadAndLevelBehavior clientWorld
            ]
            @
            Level.updates clientWorld
            @
            [
               Player.fixedUpdate
               SpriteAnimation.update
               Sprite.update am

               addRigidBodyBehavior clientWorld
               physicsSpriteBehavior clientWorld
             //  physicsUpdateBehavior clientWorld
            ]
        )
