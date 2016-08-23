[<RequireQualifiedAccess>]
module Foom.Wad.Triangulation.EarClipping

open Foom.Geometry

val compute : Polygon2D -> Triangle2D [] option

val computeTree : Polygon2DTree -> Triangle2D [] seq