namespace Foom.Geometry

open System.Numerics

[<Struct>]
type Triangle2D =

    val mutable P1 : Vector2
    val mutable P2 : Vector2
    val mutable P3 : Vector2

    new : Vector2 * Vector2 * Vector2 -> Triangle2D

    member Intersects : AABB2D -> bool

    member Contains : Vector2 -> bool

    member Area : unit -> single

    member BoundingBox : unit -> AABB2D

module Triangle2D =

    val inline area : Triangle2D -> float32
