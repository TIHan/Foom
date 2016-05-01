namespace Foom.Wad.Numerics

open System
open System.Numerics

[<RequireQualifiedAccess>]
module internal Vec2 =
    let angle (v: Vector2) =
        -(single <| atan2 (float -v.Y) (float v.X)) + single Math.PI * 0.5f
