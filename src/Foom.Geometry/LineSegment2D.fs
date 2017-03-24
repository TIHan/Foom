namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D = 

    val mutable A : Vector2
    val mutable B : Vector2

    new (a, b) =
        { A = a; B = b }

module LineSegment2D =

    // From Book: Real-Time Collision Detection
    // Modified, so it might not work :(
    let intersectsAABB (aabb : AABB2D) (seg : LineSegment2D) =
        let a = seg.A
        let b = seg.B

        let c = aabb.Center
        let e = aabb.Extents
        let m = a + b * 0.5f
        let d = b - m
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

    let aabb (seg : LineSegment2D) =
        let a = seg.A
        let b = seg.B

        let mutable minX = a.X
        let mutable maxX = a.X
        let mutable minY = a.Y
        let mutable maxY = a.Y

        if b.X < minX then minX <- b.X
        if b.X > maxX then maxX <- b.X
        if b.Y < minY then minY <- b.Y
        if b.Y > maxY then maxY <- b.Y

        AABB2D.ofMinAndMax (Vector2 (minX, minY)) (Vector2 (maxX, maxY))

    let findClosestPointByPoint (p : Vector2) (seg : LineSegment2D) =
        let a = seg.A
        let b = seg.B

        let ab = b - a

        let t = Vec2.dot (p - a) ab
        if (t <= 0.f) then
            (0.f, a)
        else
            let denom = Vec2.dot ab ab
            if (t >= denom) then
                (1.f, b)
            else
                let t = t / denom
                (t, a + (t * ab))

    let normal (seg : LineSegment2D) =
        let dx = seg.B.X - seg.A.X
        let dy = seg.B.Y - seg.A.Y

        let dir1 =
            Vector2 (-dy, dx)
            |> Vector2.Normalize

        let dir2 =
            Vector2 (dy, -dx)
            |> Vector2.Normalize

        dir2

    let inline isPointOnLeftSide (p : Vector2) (seg : LineSegment2D) =
        let v1 = seg.A
        let v2 = seg.B
        (v2.X - v1.X) * (p.Y - v1.Y) - (v2.Y - v1.Y) * (p.X - v1.X) > 0.f

    let inline startPoint (seg : LineSegment2D) = seg.A

    let inline endPoint (seg : LineSegment2D) = seg.B
