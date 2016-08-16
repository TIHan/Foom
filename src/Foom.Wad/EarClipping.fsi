[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open Foom.Wad.Geometry

val compute : Polygon2D -> Triangle2D [] option

val computeTree : Polygon2DTree -> Triangle2D [] seq