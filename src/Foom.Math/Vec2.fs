namespace Foom.Math

open System.Numerics

[<RequireQualifiedAccess>]
module Vec2 =

    let inline dot v0 v1 = Vector2.Dot (v0, v1)
