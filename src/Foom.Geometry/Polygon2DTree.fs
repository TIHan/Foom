namespace Foom.Geometry

open System.Numerics

type Polygon2DTree = 
    {
        Polygon: Polygon2D
        Children: Polygon2DTree list
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon2DTree =

    let rec containsPoint p tree =
        let result = Polygon2D.containsPoint p tree.Polygon

        if result then
            tree.Children
            |> List.forall (fun tree ->
                if Polygon2D.containsPoint p tree.Polygon then
                    tree.Children |> List.exists (containsPoint p)
                else
                    true
            )
        else
            false
