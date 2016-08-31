namespace Foom.Geometry

open System.Numerics

[<Struct>]
type Triangle2D =

    val A : Vector2

    val B : Vector2

    val C : Vector2

    new : Vector2 * Vector2 * Vector2 -> Triangle2D

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Triangle2D =

    val inline area : Triangle2D -> float32

    val containsPoint : Vector2 -> Triangle2D -> bool

    val aabb : Triangle2D -> AABB2D
