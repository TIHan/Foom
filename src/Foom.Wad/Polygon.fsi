namespace Foom.Wad.Geometry

open System.Numerics

[<Struct>]
type Edge = 
    val X : Vector2
    val Y : Vector2

type Polygon 

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon =
    val create : vertices: Vector2 [] -> Polygon

    val vertices : poly: Polygon -> Vector2 []

    val edges : poly: Polygon -> Edge []

    val isArrangedClockwise : poly: Polygon -> bool

    val isPointInside : point: Vector2 -> poly: Polygon -> bool

type PolygonTree = 
    {
        Polygon: Polygon
        Children: PolygonTree list
    }