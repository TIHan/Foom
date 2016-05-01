[<RequireQualifiedAccess>]
module Foom.Shared.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Shared.Geometry

let compute (poly: Polygon) =
    List.empty<Vector3 []>