namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type Circle2D =
    {
        center : Vector2
        radius : single
    }

    member this.Center = this.center

    member this.Radius = this.radius

module Circle2D =

    let create center radius =
        if radius < 0.f then
            failwith "Circle2D: Radius cannot be less than 0."

        { center = center; radius = radius }

    let inline center (circle : Circle2D) = circle.Center

    let inline radius (circle : Circle2D) = circle.Radius

    let aabb (circle : Circle2D) =
        AABB2D.ofCenterAndExtents circle.Center (Vector2 (circle.Radius, circle.Radius))