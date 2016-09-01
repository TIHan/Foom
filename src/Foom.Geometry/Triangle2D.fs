namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type Triangle2D =

    val A : Vector2

    val B : Vector2

    val C : Vector2

    new (a, b, c) = { A = a; B = b; C = c } 

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Triangle2D =

    let inline area (tri: Triangle2D) =
        (tri.A.X - tri.B.X) * (tri.B.Y - tri.C.Y) - (tri.B.X-tri.C.X) * (tri.A.Y - tri.B.Y)

    // From book: Real-Time Collision Detection - Pages 47-48
    // Note: "If several points are tested against the same triangle, the terms d00, d01, d11, and
    //     denom only have to be computed once, as they are fixed for a given triangle."
    let containsPoint (p: Vector2) (tri: Triangle2D) =
        // ************************
        // Barycentric
        //     "bary" comes from Greek, meaning weight.
        // ************************
        let v0 = tri.B - tri.A
        let v1 = tri.C - tri.A
        let v2 = p - tri.A

        let d00 = Vec2.dot v0 v0
        let d01 = Vec2.dot v0 v1
        let d11 = Vec2.dot v1 v1
        let d20 = Vec2.dot v2 v0
        let d21 = Vec2.dot v2 v1

        let denom = d00 * d11 - d01 * d01

        let v = (d11 * d20 - d01 * d21) / denom
        let w = (d00 * d21 - d01 * d20) / denom
        let u = 1.f - v - w
        // ************************

        0.f <= u && u <= 1.f && 0.f <= v && v <= 1.f && 0.f <= w && w <= 1.f

    let aabb (tri: Triangle2D) =
        let mutable minX = tri.A.X
        let mutable maxX = tri.A.X
        let mutable minY = tri.A.Y
        let mutable maxY = tri.A.Y

        if tri.B.X < minX then minX <- tri.B.X
        if tri.B.X > maxX then maxX <- tri.B.X
        if tri.B.Y < minY then minY <- tri.B.Y
        if tri.B.Y > maxY then maxY <- tri.B.Y

        if tri.C.X < minX then minX <- tri.C.X
        if tri.C.X > maxX then maxX <- tri.C.X
        if tri.C.Y < minY then minY <- tri.C.Y
        if tri.C.Y > maxY then maxY <- tri.C.Y

        AABB2D.ofMinAndMax (Vector2 (minX, minY)) (Vector2 (maxX, maxY))

    // This isn't efficient yet.
    let intersectsAABB (aabb: AABB2D) (tri: Triangle2D) =
        let l0 = LineSegment2D (tri.A, tri.B)
        let l1 = LineSegment2D (tri.B, tri.C)
        let l2 = LineSegment2D (tri.C, tri.A)

        LineSegment2D.intersectsAABB aabb l0 ||
        LineSegment2D.intersectsAABB aabb l1 ||
        LineSegment2D.intersectsAABB aabb l2