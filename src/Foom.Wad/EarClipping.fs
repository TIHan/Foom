[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Wad.Geometry

let compute (poly: Polygon) =
    List.empty<Vector3 []>