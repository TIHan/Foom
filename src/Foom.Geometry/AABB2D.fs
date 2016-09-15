namespace Foom.Geometry

open System.Numerics

type AABB2D =
    {
        center: Vector2
        extents: Vector2
    }

    member this.Center = this.center

    member this.Extents = this.extents

    member this.Min () = this.Center - this.Extents

    member this.Max () = this.Center + this.Extents

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module AABB2D =

    let inline center (b: AABB2D) = b.Center

    let inline extents (b: AABB2D) = b.Extents

    let inline min (b: AABB2D) = b.Min ()

    let inline max (b: AABB2D) = b.Max ()

    let ofMinAndMax (min: Vector2) (max: Vector2) =
        {
            center = (min + max) * 0.5f
            extents = (min - max) * 0.5f |> abs
        }

    let ofCenterAndExtents center extents =
        {
            center = center
            extents = extents
        }

    let intersects (a: AABB2D) (b: AABB2D) =
        if      abs (a.Center.X - b.Center.X) > (a.Extents.X + b.Extents.X) then false
        elif    abs (a.Center.Y - b.Center.Y) > (a.Extents.Y + b.Extents.Y) then false
        else    true

    let containsPoint (p: Vector2) (b: AABB2D) =
        let d = b.Center - p

        abs d.X <= b.Extents.X &&
        abs d.Y <= b.Extents.Y

    // TODO: Optimize this.
    let merge (a: AABB2D) (b: AABB2D) =
        let minA = a.Min ()
        let maxA = a.Max ()
        let minB = b.Min ()
        let maxB = b.Max ()

        let mutable minX = minA.X
        let mutable maxX = maxA.X
        let mutable minY = minA.Y
        let mutable maxY = maxA.Y

        if (minB.X < minX) then minX <- minB.X
        if (minB.Y < minY) then minY <- minB.Y
        if (maxB.X > maxX) then maxX <- maxB.X
        if (maxB.Y > maxY) then maxY <- maxB.Y

        ofMinAndMax (Vector2 (minX, minY)) (Vector2 (maxX, maxY))

