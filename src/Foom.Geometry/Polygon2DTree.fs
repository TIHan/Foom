namespace Foom.Geometry

open System.Numerics

type Polygon2DTree = 
    {
        Polygon: Polygon2D
        Children: Polygon2DTree list
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon2DTree =

    let rec contains p tree =
        let containsPoint = Polygon2D.isPointInside p tree.Polygon

        if containsPoint then
            tree.Children
            |> List.forall (fun tree ->
                if Polygon2D.isPointInside p tree.Polygon then
                    tree.Children |> List.exists (contains p)
                else
                    true
            )
        else
            false
