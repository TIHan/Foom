namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

[<Struct>]
type Hash =

    val X : int

    val Y : int

    new (x, y) = { X = x; Y = y }

type RigidBody (shape, position: Vector3) =

    let aabb =
        match shape with
        | DynamicAABB aabb -> aabb.AABB
        | StaticWall staticWall -> LineSegment2D.aabb staticWall.LineSegment

    member val Id = 0 with get, set

    member val AABB = aabb with get

    member val Shape : CollisionShape = shape with get

    member val Hashes = HashSet<Hash> () with get

    member val WorldPosition = Vector2 (position.X, position.Y) with get, set

    member val Z = position.Z with get, set
