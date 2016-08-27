namespace Foom.Geometry

open System.Numerics

type ContainmentType =
    | Disjoint
    | Contains
    | Intersects

type AABB2D =
    {
        Min: Vector2
        Max: Vector2
    }

    member Center : Vector2

    member HalfSize : Vector2

    static member FromCenterAndHalfSize : Vector2 * Vector2 -> AABB2D

    member Contains : Vector2 -> ContainmentType

    member Contains : AABB2D -> ContainmentType
