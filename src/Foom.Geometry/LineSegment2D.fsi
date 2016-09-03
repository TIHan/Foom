namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D =

    val A : Vector2

    val B : Vector2

    new : Vector2 * Vector2 -> LineSegment2D

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module LineSegment2D =

    val intersectsAABB : AABB2D -> LineSegment2D -> bool

    val aabb : LineSegment2D -> AABB2D