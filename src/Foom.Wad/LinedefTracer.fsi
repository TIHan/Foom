namespace Foom.Wad

open System.Numerics
open System.Collections.Immutable

open Foom.Geometry

type LinedefPolygon = 
    {
        Linedefs: Linedef list
        Inner: LinedefPolygon list
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module LinedefTracer =
    //val run : Linedef seq -> Linedef list list
    val run2 : Linedef seq -> int -> LinedefPolygon list

module Polygon =

    val ofLinedefs : Linedef list -> int -> Polygon2D