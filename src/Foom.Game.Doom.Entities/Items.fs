namespace Foom.Game.Gameplay.Doom

open System
open System.Numerics

open Foom.Ecs
open Foom.Physics

open Foom.Game.Core
open Foom.Game.Assets
open Foom.Game.Sprite

module ArmorBonus =

    let texture = TextureKind.Multi [ "BON2A0.png"; "BON2B0.png"; "BON2C0.png"; "BON2D0.png" ]

    let spawn position =
        entity {
            add (TransformComponent (Matrix4x4.CreateTranslation (position)))
            add (SpriteComponent (0, texture, 255))
            add (SpriteAnimationComponent (TimeSpan.FromSeconds 1., [ 0; 1; 2; 3; 2; 1 ]))
            add (RigidBodyComponent (position, 20.f, 16.f))
        }

module GreenArmor =

    let texture = TextureKind.Multi [ "ARM1A0.png"; "ARM1B0.png" ]

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent (0, texture, 255))
        let interval = TimeSpan.FromSeconds(0.5)
        em.Add (ent, SpriteAnimationComponent (interval, [ 0; 1 ]))
        em.Add (ent, RigidBodyComponent(position, 20.f, 16.f))
        ent

module BlueArmor =

    let texture = TextureKind.Multi [ "ARM2A0.png"; "ARM2B0.png" ]

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent (0, texture, 255))
        let interval = TimeSpan.FromSeconds(0.5)
        em.Add (ent, SpriteAnimationComponent (interval, [ 0; 1 ]))
        em.Add (ent, RigidBodyComponent(position, 20.f, 16.f))
        ent

module SoulSphere =

    let texture = TextureKind.Multi [ "SOULA0.png"; "SOULB0.png"; "SOULC0.png"; "SOULD0.png" ]

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent (0, texture, 255))
        let interval = TimeSpan.FromSeconds(1.)
        em.Add (ent, SpriteAnimationComponent (interval, [ 0; 1; 2; 3; 2; 1 ]))
        em.Add (ent, RigidBodyComponent(position, 20.f, 16.f))
        ent