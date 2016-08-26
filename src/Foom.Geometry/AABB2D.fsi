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

    member Contains : Vector2 -> ContainmentType

    member Contains : AABB2D -> ContainmentType
