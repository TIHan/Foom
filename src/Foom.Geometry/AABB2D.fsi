namespace Foom.Geometry

open System.Numerics

[<Sealed>]
type AABB2D =

    member Center : Vector2

    member Extents : Vector2

    member Min : unit -> Vector2

    member Max : unit -> Vector2

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module AABB2D =

    val inline center : AABB2D -> Vector2

    val inline extents : AABB2D -> Vector2

    val inline min : AABB2D -> Vector2

    val inline max : AABB2D -> Vector2

    val ofMinAndMax : Vector2 -> Vector2 -> AABB2D

    val ofCenterAndExtents : Vector2 -> Vector2 -> AABB2D

    val intersects : AABB2D -> AABB2D -> bool

    val containsPoint : Vector2 -> AABB2D -> bool