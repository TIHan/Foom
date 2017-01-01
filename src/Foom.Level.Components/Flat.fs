namespace Foom.Level

open System.Numerics

open Foom.Geometry
open Foom.Wad.Level

type Flat =
    {
        SectorId: int
        Ceiling: FlatPart
        Floor: FlatPart
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Flat =
   
    let createFlats sectorId level = 
        match level |> Level.tryGetSector sectorId with
        | None -> Seq.empty
        | Some sector ->

            match LinedefTracer.run2 (sector.Linedefs) sectorId with
            | [] -> Seq.empty
            | linedefPolygons ->
                let rec map (linedefPolygons: LinedefPolygon list) =
                    linedefPolygons
                    |> List.map (fun x -> 
                        {
                            Polygon = (x.Linedefs, sectorId) ||> Polygon.ofLinedefs
                            Children = map x.Inner
                        }
                    )

                let sectorTriangles = 
                    map linedefPolygons
                    |> Seq.map (Foom.Wad.Triangulation.EarClipping.computeTree)
                    |> Seq.reduce Seq.append
                    |> Array.ofSeq

                sectorTriangles
                |> Seq.filter (fun x -> x.Length <> 0)
                |> Seq.map (fun triangles ->
                  
                    let ceiling =
                        FlatPart.create
                            (
                                triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.C.X, tri.C.Y, single sector.CeilingHeight)
                                        Vector3 (tri.B.X, tri.B.Y, single sector.CeilingHeight)
                                        Vector3 (tri.A.X, tri.A.Y, single sector.CeilingHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append
                            )
                            (float32 sector.CeilingHeight)
                            (Some sector.CeilingTextureName)

                    let floor =
                        FlatPart.create
                            (
                                triangles
                                |> Seq.map (fun tri ->
                                    [|
                                        Vector3 (tri.A.X, tri.A.Y, single sector.FloorHeight)
                                        Vector3 (tri.B.X, tri.B.Y, single sector.FloorHeight)
                                        Vector3 (tri.C.X, tri.C.Y, single sector.FloorHeight)
                                    |]
                                )
                                |> Seq.reduce Array.append
                            )
                            (float32 sector.FloorHeight)
                            (Some sector.FloorTextureName)

                    {
                        SectorId = sectorId
                        Ceiling = ceiling
                        Floor = floor
                    }
                )
