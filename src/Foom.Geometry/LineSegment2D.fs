namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type LineSegment2D = 
    LineSegment2D of Vector2 * Vector2 with

    member inline this.A =
        match this with
        | LineSegment2D (a, _) -> a

    member inline this.B =
        match this with
        | LineSegment2D (_, b) -> b

module LineSegment2D =

    // From Book: Real-Time Collision Detection
    // Modified, so it might not work :(
    let intersectsAABB (aabb: AABB2D) (LineSegment2D (a, b)) =
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

    let aabb (LineSegment2D (a, b)) =
        let mutable minX = a.X
        let mutable maxX = a.X
        let mutable minY = a.Y
        let mutable maxY = a.Y

        if b.X < minX then minX <- b.X
        if b.X > maxX then maxX <- b.X
        if b.Y < minY then minY <- b.Y
        if b.Y > maxY then maxY <- b.Y

        AABB2D.ofMinAndMax (Vector2 (minX, minY)) (Vector2 (maxX, maxY))

    let findClosestPointByPoint (p: Vector2) (LineSegment2D (b, a)) =
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

    let normal (LineSegment2D (a, b)) =
        let dx = b.X - a.X
        let dy = b.Y - a.Y

        let dir1 =
            Vector2 (-dy, dx)
            |> Vector2.Normalize

        let dir2 =
            Vector2 (dy, -dx)
            |> Vector2.Normalize

        dir2

    let inline isPointOnLeftSide (p: Vector2) (LineSegment2D (v1, v2)) =
        (v2.X - v1.X) * (p.Y - v1.Y) - (v2.Y - v1.Y) * (p.X - v1.X) > 0.f

    let inline startPoint (LineSegment2D (a, _)) = a

    let inline endPoint (LineSegment2D (_, b)) = b
