namespace Foom.Shared.Geometry

open System.Numerics

[<Struct>]
type Edge = 
    val X : Vector2
    val Y : Vector2

// TODO: PolygonTree might be better IMO.
type Polygon = { Vertices: Vector2 []; Children: Polygon list }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon =
    val inline create : vertices: Vector2 [] -> Polygon

    val inline addChild : child: Polygon -> poly: Polygon -> Polygon

    val inline addChildren : children: Polygon list -> poly: Polygon -> Polygon

    val inline vertices : poly: Polygon -> Vector2 []

    val inline children : poly: Polygon -> Polygon list

    val edges : poly: Polygon -> Edge list

    val isArrangedClockwise : poly: Polygon -> bool

    val isPointInside : point: Vector2 -> poly: Polygon -> bool