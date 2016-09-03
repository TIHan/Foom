namespace Foom.Geometry

open System.Numerics

open Foom.Math

[<Struct>]
type Circle2D =

    val Center : Vector2

    val Radius : float32

    new (center, radius) = { Center = center; Radius = radius }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Circle2D =

    let inline center (circle: Circle2D) = circle.Center

    let inline radius (circle: Circle2D) = circle.Radius