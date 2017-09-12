namespace Foom.Physics

open System.Numerics
open System.Runtime.Serialization
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

    member this.Position
        with get () = Vector3 (rigidBody.WorldPosition, rigidBody.Z)

    member this.Radius = radius

    member this.Height = height

    [<IgnoreDataMember>]
    member this.RigidBody = rigidBody

type RigidBodyComponent (position: Vector3, radius: float32, height: float32) =
    inherit Component ()

    let aabb = AABB2D.ofCenterAndExtents Vector2.Zero (Vector2 (radius, radius))
    let dynamicAABB = { AABB = aabb; Height = height }
    let rigidBody =
        RigidBody (CollisionShape.DynamicAABB dynamicAABB, position)

    member this.Position
        with get () = Vector3 (rigidBody.WorldPosition, rigidBody.Z)

    member this.Radius = radius

    member this.Height = height

    [<IgnoreDataMember>]
    member this.RigidBody = rigidBody
