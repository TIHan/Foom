namespace Foom.Physics

open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Ecs
open Foom.Geometry

type CharacterControllerComponent (position: Vector3, radius: float32, height: float32) =
    inherit Component ()

    let aabb = AABB2D.ofCenterAndExtents Vector2.Zero (Vector2 (radius, radius))
    let dynamicAABB = { AABB = aabb; Height = height }
    let rigidBody =
        RigidBody (CollisionShape.DynamicAABB dynamicAABB, position)

    member this.RigidBody = rigidBody

type RigidBodyComponent (position: Vector3, radius: float32, height: float32) =
    inherit Component ()

    let aabb = AABB2D.ofCenterAndExtents Vector2.Zero (Vector2 (radius, radius))
    let dynamicAABB = { AABB = aabb; Height = height }
    let rigidBody =
        RigidBody (CollisionShape.DynamicAABB dynamicAABB, position)

    member this.RigidBody = rigidBody

type PhysicsEngineComponent (cellSize) =
    inherit Component ()

    member val PhysicsEngine = PhysicsEngine.create cellSize
