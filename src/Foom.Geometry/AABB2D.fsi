namespace Foom.Geometry

open System.Numerics

type ContainmentType =
    | Disjoint
    | Contains
    | Intersects

[<Sealed>]
type AABB2D =

    member Min : Vector2

    member Max : Vector2

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module AABB2D =

    val inline min : AABB2D -> Vector2

    val inline max : AABB2D -> Vector2

    val ofMinAndMax : Vector2 -> Vector2 -> AABB2D

    val containsPoint : Vector2 -> AABB2D -> ContainmentType

    val containsAABB : AABB2D -> AABB2D -> ContainmentType
