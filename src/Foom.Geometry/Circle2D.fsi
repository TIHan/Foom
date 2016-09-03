namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type Circle2D =

    val Center : Vector2

    val Radius : float32

    new : Vector2 * float32 -> Circle2D

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Circle2D =

    val inline center : Circle2D -> Vector2

    val inline radius : Circle2D -> float32
