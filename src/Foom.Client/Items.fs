namespace Foom.Game.Gameplay.Doom

open System
open System.Numerics

open Foom.Ecs
open Foom.Physics

open Foom.Game.Core
open Foom.Game.Assets
open Foom.Game.Sprite

module ArmorBonus =

    let texture = Texture (TextureKind.Multi [ "BON2A0.bmp"; "BON2B0.bmp"; "BON2C0.bmp"; "BON2D0.bmp" ])

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent ("World", texture, 255))
        let interval = TimeSpan.FromSeconds(1.)
        em.Add (ent, SpriteAnimationComponent (interval, [ 0; 1; 2; 3; 2; 1 ]))
        em.Add (ent, RigidBodyComponent(position, 20.f, 16.f))
        ent

module GreenArmor =

    let texture = Texture (TextureKind.Multi [ "ARM1A0.bmp"; "ARM1B0.bmp" ])

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent ("World", texture, 255))
        let interval = TimeSpan.FromSeconds(0.5)
        em.Add (ent, SpriteAnimationComponent (interval, [ 0; 1 ]))
        em.Add (ent, RigidBodyComponent(position, 20.f, 16.f))
        ent
