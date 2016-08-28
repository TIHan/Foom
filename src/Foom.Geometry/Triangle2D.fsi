namespace Foom.Geometry

open System.Numerics

[<Struct>]
type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new : Vector2 * Vector2 * Vector2 -> Triangle2D

    member Contains : Vector2 -> bool
