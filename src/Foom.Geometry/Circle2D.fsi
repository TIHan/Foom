namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type Circle2D =
    private {
        center : Vector2
        radius : single
    }

    member Center : Vector2

    member Radius : single

module Circle2D =

    val create : center : Vector2 -> radius : single -> Circle2D

    val inline center : Circle2D -> Vector2

    val inline radius : Circle2D -> float32

    val aabb : Circle2D -> AABB2D
