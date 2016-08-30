namespace Foom.Geometry

open System.Numerics

type ContainmentType =
    | Disjoint
    | Contains
    | Intersects

type AABB2D =
    {
        min: Vector2
        max: Vector2
    }

    member this.Min = this.min

    member this.Max = this.max

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module AABB2D =

    let inline min (b: AABB2D) = b.Min

    let inline max (b: AABB2D) = b.Max

    let ofMinAndMax min max =
        {
            min = min
            max = max
        }

    let containsPoint (point: Vector2) (b: AABB2D) =
        //first we get if point is out of box
        if (point.X < b.Min.X
            || point.X > b.Max.X
            || point.Y < b.Min.Y
            || point.Y > b.Max.Y) then
            ContainmentType.Disjoint

        //or if point is on box because coordonate of point is lesser or equal
        elif (point.X = b.Min.X
            || point.X = b.Max.X
            || point.Y = b.Min.Y
            || point.Y = b.Max.Y) then
            ContainmentType.Intersects
        else
            ContainmentType.Contains

    let containsAABB (b1: AABB2D) (b: AABB2D) =
        //test if all corner is in the same side of a face by just checking min and max
        if (b1.Max.X < b.Min.X
            || b1.Min.X > b.Max.X
            || b1.Max.Y < b.Min.Y
            || b1.Min.Y > b.Max.Y) then
            ContainmentType.Disjoint

        elif (b1.Min.X >= b.Min.X
            && b1.Max.X <= b.Max.X
            && b1.Min.Y >= b.Min.Y
            && b1.Max.Y <= b.Max.Y) then
            ContainmentType.Contains

        else
            ContainmentType.Intersects
