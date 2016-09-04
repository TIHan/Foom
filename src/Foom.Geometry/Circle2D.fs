namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type Circle2D =

    val mutable Center : Vector2

    val Radius : float32

    new (center, radius) = { Center = center; Radius = radius }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Circle2D =

    let inline center (circle: Circle2D) = circle.Center

    let inline radius (circle: Circle2D) = circle.Radius

    let aabb (circle: Circle2D) =
        AABB2D.ofCenterAndExtents circle.Center (Vector2 (circle.Radius, circle.Radius))