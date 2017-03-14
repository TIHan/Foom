namespace Foom.Game.Gameplay.Doom

open System
open System.Numerics

open Foom.Ecs
open Foom.Physics

open Foom.Game.Core
open Foom.Game.Assets
open Foom.Game.Sprite

module ShotgunGuy =

    let texture = Texture (TextureKind.Multi [ "SPOSA1.bmp"; "SPOSB1.bmp" ])

    let spawn position (em: EntityManager) =

        let ent = em.Spawn ()
        em.Add (ent, TransformComponent (Matrix4x4.CreateTranslation(position)))
        em.Add (ent, SpriteComponent ("World", texture, 255))
        let interval = TimeSpan.FromSeconds(0.65)
        em.Add (ent, AnimatedSpriteComponent (interval, [ 0; 1 ]))
        em.Add (ent, RigidBodyComponent(position, 20.f, 56.f))
        ent

