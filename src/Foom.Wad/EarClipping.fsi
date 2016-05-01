[<RequireQualifiedAccess>]
module Foom.Shared.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Shared.Geometry

val compute : Polygon -> (Vector3 []) list