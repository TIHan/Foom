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