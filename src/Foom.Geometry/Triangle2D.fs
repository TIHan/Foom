namespace Foom.Geometry

open System.Numerics

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
        // ************************
        let v0 = tri.B - tri.A
        let v1 = tri.C - tri.A
        let v2 = p - tri.A

        let d00 = Vector2.Dot (v0, v0)
        let d01 = Vector2.Dot (v0, v1)
        let d11 = Vector2.Dot (v1, v1)
        let d20 = Vector2.Dot (v2, v0)
        let d21 = Vector2.Dot (v2, v1)

        let denom = d00 * d11 - d01 * d01

        let v = (d11 * d20 - d01 * d21) / denom
        let w = (d00 * d21 - d01 * d20) / denom
        let u = 1.f - v - w
        // ************************

        0.f <= u && u <= 1.f && 0.f <= v && v <= 1.f && 0.f <= w && w <= 1.f
