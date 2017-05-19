namespace Foom.Game.Gameplay.Doom

open System
open System.Numerics

open Foom.Ecs
open Foom.Physics

open Foom.Game.Core
open Foom.Game.Assets
open Foom.Game.Sprite

module ShotgunGuy =

    let texture = TextureKind.Multi [ "SPOSA1.png"; "SPOSB1.png" ]

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent (0, texture, 255))
        em.Add (ent, SpriteAnimationComponent (TimeSpan.FromSeconds(0.65), [ 0; 1 ]))
        em.Add (ent, RigidBodyComponent(position, 20.f, 56.f))
        ent

