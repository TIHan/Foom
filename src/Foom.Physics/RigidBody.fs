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

type RigidBody (shape, position: Vector3, height) =

    let aabb =
        match shape with
        | Circle circle -> circle |> Circle2D.aabb
        | AABB aabb -> aabb

    member val Id = 0 with get, set

    member val AABB = aabb with get

    member val Shape : CollisionShape = shape with get

    member val Hashes = HashSet<Hash> () with get

    member val WorldPosition = Vector2 (position.X, position.Y) with get, set

    member val Z = position.Z with get, set

    member val Height : float32 = height with get
