namespace Foom.Geometry

open System.Numerics

[<Struct>]
type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new (x, y, z) = { X = x; Y = y; Z = z } 

    // http://totologic.blogspot.com/2014/01/accurate-point-in-triangle-test.html
    member this.Contains (point: Vector2) =
        let x = point.X
        let y = point.Y
        let x1 = this.X.X
        let x2 = this.Y.X
        let x3 = this.Z.X
        let y1 = this.X.Y
        let y2 = this.Y.Y
        let y3 = this.Z.Y

        let denominator = ((y2 - y3)*(x1 - x3) + (x3 - x2)*(y1 - y3))
        let a = ((y2 - y3)*(x - x3) + (x3 - x2)*(y - y3)) / denominator;
        let b = ((y3 - y1)*(x - x3) + (x1 - x3)*(y - y3)) / denominator;
        let c = 1.f - a - b;

        0.f <= a && a <= 1.f && 0.f <= b && b <= 1.f && 0.f <= c && c <= 1.f;
