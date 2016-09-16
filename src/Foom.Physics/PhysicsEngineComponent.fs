namespace Foom.Physics

open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Ecs
open Foom.Geometry

type CharacterControllerComponent (position: Vector3, radius: float32, height: float32) =

    let aabb = AABB2D.ofCenterAndExtents Vector2.Zero (Vector2 (radius, radius))
    let rigidBody =
        RigidBody (CollisionShape.AABB aabb, position, height)

    member this.RigidBody = rigidBody

    interface IComponent

type PhysicsEngineComponent =
    {
        PhysicsEngine: PhysicsEngine
    }

    static member Create (cellSize) =
        {
            PhysicsEngine = PhysicsEngine.create cellSize
        }

    interface IComponent
