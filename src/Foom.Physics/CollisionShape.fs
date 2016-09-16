namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

type DynamicAABB =
    {
        AABB: AABB2D
        Height: float32
    }

type StaticWall =
    {
        LineSegment: LineSegment2D
        IsTrigger: bool
    }

type CollisionShape =
    | DynamicAABB of DynamicAABB
    | StaticWall of StaticWall
