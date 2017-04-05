namespace Foom.Game.Gameplay.Doom

open System
open System.Numerics

open Foom.Ecs
open Foom.Physics

open Foom.Game.Core
open Foom.Game.Assets
open Foom.Game.Sprite

module GibbedMarine =

    let texture = Texture (TextureKind.Single "PLAYW0.png")

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent (RenderGroup.World, texture, 255))
        em.Add (ent, RigidBodyComponent(position, 1.f, 1.f))
        ent
