namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D =

    val A : Vector2

    val B : Vector2

    new (a, b) = { A = a; B = b }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module LineSegment2D =

    // From Book: Real-Time Collision Detection
    // Modified, so it might not work :(
    let intersectsAABB (aabb: AABB2D) (seg: LineSegment2D) =
        let c = aabb.Center
        let e = aabb.Extents
        let m = (seg.A + seg.B) * 0.5f
        let d = seg.B - m
        let m = m - c

        let adx = abs d.X
        if abs m.X > e.X + adx then false
        else

        let ady = abs d.Y
        if abs m.Y > e.Y + ady then false
        else

        let adx = adx + System.Single.Epsilon
        let ady = ady + System.Single.Epsilon

        if abs (m.X * d.Y - m.Y * d.X) > e.X * ady + e.Y * adx then false
        else
            true

    let aabb (seg: LineSegment2D) =
        let mutable minX = seg.A.X
        let mutable maxX = seg.A.X
        let mutable minY = seg.A.Y
        let mutable maxY = seg.A.Y

        if seg.B.X < minX then minX <- seg.B.X
        if seg.B.X > maxX then maxX <- seg.B.X
        if seg.B.Y < minY then minY <- seg.B.Y
        if seg.B.Y > maxY then maxY <- seg.B.Y

        AABB2D.ofMinAndMax (Vector2 (minX, minY)) (Vector2 (maxX, maxY))

    let findClosestPointByPoint (p: Vector2) (seg: LineSegment2D) =
        let ab = seg.B - seg.A

        let t = Vec2.dot (p - seg.A) ab
        if (t <= 0.f) then
            (0.f, seg.A)
        else
            let denom = Vec2.dot ab ab
            if (t >= denom) then
                (1.f, seg.B)
            else
                let t = t / denom
                (t, seg.A + (t * ab))

    let normal (seg: LineSegment2D) =
        let dx = seg.B.X - seg.A.X
        let dy = seg.B.Y - seg.A.Y

        let dir1 =
            Vector2 (-dy, dx)
            |> Vector2.Normalize

        let dir2 =
            Vector2 (dy, -dx)
            |> Vector2.Normalize

        dir2