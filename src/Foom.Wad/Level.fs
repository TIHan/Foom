namespace Foom.Wad.Level

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type Level = 
    {
        Sectors: Sector [] 
    }

    member this.CalculatePolygonTrees () =
        this.Sectors
        |> Array.map (fun sector -> LinedefTracer.run sector.Linedefs)
        |> Array.map (fun sectorLinedefs ->
            let polygons =
                sectorLinedefs
                |> List.map Polygon.ofLinedefs
                
            let rec calc (polygons: Polygon list) (trees: PolygonTree list) =
                match trees, polygons with
                | _, [] ->
                    trees

                | [], poly :: polygons -> 
                    calc polygons [PolygonTree (poly, [])]

                | _, poly :: polygons ->
                    trees
                    |> List.map (fun (PolygonTree (poly2, children) as tree) ->
                        if (Polygon.isPointInside (Polygon.vertices poly).[0] poly2) then
                            [PolygonTree (poly2, calc [poly] children)]
                        elif (Polygon.isPointInside (Polygon.vertices poly2).[0] poly) then
                            [PolygonTree (poly, [tree])]
                        else
                            PolygonTree (poly, []) :: [tree]
                    )
                    |> List.reduce (@)
                    |> calc polygons
            calc polygons []
        )
        |> Array.reduce (@)