namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D =
    {
        A: Vector2
        B: Vector2
    }

module LineSegment2D =

    val intersectsAABB : AABB2D -> LineSegment2D -> bool

    val aabb : LineSegment2D -> AABB2D

    val findClosestPointByPoint : Vector2 -> LineSegment2D -> float32 * Vector2

    val normal : LineSegment2D -> Vector2

    val inline isPointOnLeftSide : Vector2 -> LineSegment2D -> bool