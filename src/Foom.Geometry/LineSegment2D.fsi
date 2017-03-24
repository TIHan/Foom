namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D = 

    val mutable A : Vector2
    val mutable B : Vector2

    new : Vector2 * Vector2 -> LineSegment2D

module LineSegment2D =

    val intersectsAABB : AABB2D -> LineSegment2D -> bool

    val aabb : LineSegment2D -> AABB2D

    val findClosestPointByPoint : Vector2 -> LineSegment2D -> float32 * Vector2

    val normal : LineSegment2D -> Vector2

    val inline isPointOnLeftSide : Vector2 -> LineSegment2D -> bool

    val inline startPoint : LineSegment2D -> Vector2

    val inline endPoint : LineSegment2D -> Vector2
