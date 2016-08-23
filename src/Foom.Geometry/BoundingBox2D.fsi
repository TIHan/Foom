namespace Foom.Geometry

open System.Numerics

type ContainmentType =
    | Disjoint
    | Contains
    | Intersects

type BoundingBox2D =
    {
        Min: Vector2
        Max: Vector2
    }

    member Contains : Vector2 -> ContainmentType

    member Intersects : BoundingBox2D -> ContainmentType
