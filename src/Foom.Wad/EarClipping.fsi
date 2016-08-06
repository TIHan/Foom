[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Wad.Geometry

[<Struct>]
type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new : Vector2 * Vector2 * Vector2 -> Triangle2D

val compute : Polygon -> Triangle2D [] option

val computeTree : PolygonTree -> (Triangle2D []) list