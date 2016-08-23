namespace Foom.Geometry

open System.Numerics

type Polygon2D =
    {
        Vertices: Vector2 []
    } 

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon2D =

    val create : vertices: Vector2 [] -> Polygon2D

    val isArrangedClockwise : poly: Polygon2D -> bool

    val isPointInside : point: Vector2 -> poly: Polygon2D -> bool
