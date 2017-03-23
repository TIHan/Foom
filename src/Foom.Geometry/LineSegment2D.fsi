namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D = 
    LineSegment2D of Vector2 * Vector2 with

    member inline A : Vector2

    member inline B : Vector2

module LineSegment2D =

    val intersectsAABB : AABB2D -> LineSegment2D -> bool

    val aabb : LineSegment2D -> AABB2D

    val findClosestPointByPoint : Vector2 -> LineSegment2D -> float32 * Vector2

    val normal : LineSegment2D -> Vector2

    val inline isPointOnLeftSide : Vector2 -> LineSegment2D -> bool

    val inline startPoint : LineSegment2D -> Vector2

    val inline endPoint : LineSegment2D -> Vector2
