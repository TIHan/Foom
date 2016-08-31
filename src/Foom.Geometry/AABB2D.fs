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
            extents = (min - max) * 0.5f
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
