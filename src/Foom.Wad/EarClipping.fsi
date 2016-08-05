[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Wad.Geometry

val compute : Polygon -> Polygon list

val computeTree : PolygonTree -> Polygon list