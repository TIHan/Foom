namespace Foom.Geometry

open System.Numerics

type Polygon2DTree = 
    {
        Polygon: Polygon2D
        Children: Polygon2DTree list
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon2DTree =

    val containsPoint : Vector2 -> Polygon2DTree -> bool
