namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

[<Struct>]
type internal Hash =

    val X : int

    val Y : int

    new : int * int -> Hash

type RigidBody =

    member internal Id : int with get, set

    member AABB : AABB2D

    member internal Shape : CollisionShape

    member internal Hashes : HashSet<Hash>

    member WorldPosition : Vector2 with get

    member internal WorldPosition : Vector2 with set

    member Z : float32 with get

    member internal Z : float32 with set

    new : CollisionShape * Vector3 -> RigidBody