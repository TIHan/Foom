namespace Foom.Wad.Level.Structures

open System.Numerics

open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

type Sector = 
    {
        id: int
        linedefs: Linedef [] 
        floorTextureName: string
        floorHeight: int
        ceilingTextureName: string
        ceilingHeight: int
        lightLevel: int
    } 

    member this.Id = this.id

    member this.Linedefs = this.linedefs |> Seq.ofArray

    member this.FloorTextureName = this.floorTextureName

    member this.FloorHeight = this.floorHeight

    member this.CeilingTextureName = this.ceilingTextureName

    member this.CeilingHeight = this.ceilingHeight

    member this.LightLevel = this.lightLevel

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =

    let polygonFlats (sector: Sector) = 
        match LinedefTracer.run2 (sector.Linedefs) sector.id with
        | [] -> Seq.empty
        | linedefPolygons ->
            let rec map (linedefPolygons: LinedefPolygon list) =
                linedefPolygons
                |> List.map (fun x -> 
                    {
                        Polygon = (x.Linedefs, sector.id) ||> Polygon.ofLinedefs
                        Children = map x.Inner
                    }
                )

            map linedefPolygons
            |> Seq.map Foom.Wad.Geometry.Triangulation.EarClipping.computeTree
            |> Seq.reduce Seq.append