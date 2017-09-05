module Foom.Client.ClientSystem

open System
open System.IO
open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Physics
open Foom.Geometry
open Foom.Renderer
open Foom.Wad
open Foom.Wad.Level

open Foom.Game.Core
open Foom.Game.Sprite
open Foom.Game.Wad

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
    Behavior.HandleLatestEvent (fun (evt: LoadWadAndLevelRequested) _ em ->
        match em.TryGet<WadComponent> clientWorld.Entity with
        | None ->

            em.Add (clientWorld.Entity, WadComponent(evt.Wad))
            em.Add (clientWorld.Entity, LevelComponent(evt.Level))

        | Some _ ->

            clientWorld.DestroyEntities ()

            //em.Remove<WadComponent> clientWorld.Entity
            //em.Remove<LevelComponent> clientWorld.Entity

            em.Add (clientWorld.Entity, WadComponent(evt.Wad))
            em.Add (clientWorld.Entity, LevelComponent(evt.Level))
    )

let addRigidBodyBehavior (physics : PhysicsEngine) (clientWorld: ClientWorld) : Behavior<float32 * float32> =
    Behavior.Merge
        [
            // This should be part of a physics system.
            Behavior.ComponentAdded (fun _ _ (charContrComp: CharacterControllerComponent) (transformComp : TransformComponent) ->
                physics
                |> PhysicsEngine.addRigidBody charContrComp.RigidBody
            )

            // This should be part of a physics system.
            Behavior.ComponentAdded (fun _ _ (rbodyComp: RigidBodyComponent) (_ : TransformComponent) ->
                physics
                |> PhysicsEngine.addRigidBody rbodyComp.RigidBody
            )
    ]

let physicsSpriteBehavior (physics : PhysicsEngine) (clientWorld: ClientWorld) =
    Behavior.ComponentAdded (fun _ (ent: Entity) (spriteComp: SpriteComponent) (charContrComp : RigidBodyComponent) (transformComp : TransformComponent) ->
        // TODO: This can be null, fix it.
        let sector =
            physics
            |> PhysicsEngine.findWithPoint charContrComp.RigidBody.WorldPosition

        if obj.ReferenceEquals (sector, null) |> not then
            let sector = sector :?> Foom.Game.Level.Sector

            spriteComp.LightLevel <- sector.lightLevel
            transformComp.Position <- Vector3 (transformComp.Position.X, transformComp.Position.Y, single sector.floorHeight)
        else
            System.Diagnostics.Debug.WriteLine (String.Format ("Entity (Rigid Body), {0}, didn't spawn in a proper spot.", ent))
    )

let physicsUpdateBehavior (physics : PhysicsEngine) (clientWorld: ClientWorld) =
    Behavior.Update (fun _ em _ ->

        opt {
            //let! wireframeComp = em.TryGet<WireframeComponent> clientWorld.Entity
            let! (_, charContrComp, transformComp) = em.TryFind<CharacterControllerComponent, TransformComponent> (fun _ _ _ -> true)

            let pos = transformComp.Position
            let pos = Vector2 (pos.X, pos.Y)

            let rbody : RigidBody = charContrComp.RigidBody
            physics
            |> PhysicsEngine.moveRigidBody transformComp.Position rbody

            transformComp.Position <- Vector3 (rbody.WorldPosition, transformComp.Position.Z)

            // TODO: This can be null, fix it.
            let sector =
                physics
                |> PhysicsEngine.findWithPoint rbody.WorldPosition

            if obj.ReferenceEquals (sector, null) |> not then
                let sector = sector :?> Foom.Game.Level.Sector

                transformComp.Position <- Vector3 (rbody.WorldPosition, single sector.floorHeight + 50.f)
        } |> ignore

    )


let create openWad exportTextures (clientWorld: ClientWorld) am =
    Behavior.Delay (fun () ->
        let physics = PhysicsEngine.create 128
        Behavior.Merge
            (
                [
                    loadWadAndLevelBehavior clientWorld
                ]
                @
                Level.updates openWad exportTextures am physics clientWorld
                @
                [
                    Player.fixedUpdate

                    addRigidBodyBehavior physics clientWorld
                    physicsSpriteBehavior physics clientWorld
                    // physicsUpdateBehavior clientWorld

                    RendererSystem.assetBehavior am

                    SpriteAnimation.update
                    Sprite.update am
                ]
            )
    )
