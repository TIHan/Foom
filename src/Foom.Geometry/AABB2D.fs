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

    member this.Contains (point: Vector2) =
        //first we get if point is out of box
        if (point.X < this.Min.X
            || point.X > this.Max.X
            || point.Y < this.Min.Y
            || point.Y > this.Max.Y) then
            ContainmentType.Disjoint

        //or if point is on box because coordonate of point is lesser or equal
        elif (point.X = this.Min.X
            || point.X = this.Max.X
            || point.Y = this.Min.Y
            || point.Y = this.Max.Y) then
            ContainmentType.Intersects
        else
            ContainmentType.Contains

    member this.Contains b =
        //test if all corner is in the same side of a face by just checking min and max
        if (b.Max.X < this.Min.X
            || b.Min.X > this.Max.X
            || b.Max.Y < this.Min.Y
            || b.Min.Y > this.Max.Y) then
            ContainmentType.Disjoint

        elif (b.Min.X >= this.Min.X
            && b.Max.X <= this.Max.X
            && b.Min.Y >= this.Min.Y
            && b.Max.Y <= this.Max.Y) then
            ContainmentType.Contains

        else
            ContainmentType.Intersects
