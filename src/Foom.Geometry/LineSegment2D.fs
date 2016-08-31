namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D =

    val A : Vector2

    val B : Vector2

    new (a, b) = { A = a; B = b }
