namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

type CollisionShape =
    | Circle of Circle2D
    | AABB of AABB2D
