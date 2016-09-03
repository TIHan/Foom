namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

type LineDefinition =
    {
        FrontFaceAreaId: int
        BackFaceAreaId: int

        LineSegment: LineSegment2D
        IsWall: bool
    }

type SpatialHashBucket =
    {
        Triangles: ResizeArray<Triangle2D>
        TriangleData: ResizeArray<obj>

        LineDefinitions: ResizeArray<LineDefinition>
    }

[<Struct>]
type Hash =

    val X : int

    val Y : int

    new (x, y) = { X = x; Y = y }

type SpatialHash =
    {
        CellSize: int
        Buckets: Dictionary<Hash, SpatialHashBucket>
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module SpatialHash =

    let create cellSize =
        {
            CellSize = cellSize
            Buckets = Dictionary ()
        }

    let addTriangleHash hash tri data spatialHash =
        match spatialHash.Buckets.TryGetValue (hash) with
        | true, bucket ->
            bucket.Triangles.Add (tri)
            bucket.TriangleData.Add (data)
        | _ ->
            let bucket =
                {
                    Triangles = ResizeArray ()
                    TriangleData = ResizeArray ()
                    LineDefinitions = ResizeArray ()
                }
            bucket.Triangles.Add (tri)
            bucket.TriangleData.Add (data)
            spatialHash.Buckets.Add (hash, bucket)

    let addLineDefinitionHash hash lined spatialHash =
        match spatialHash.Buckets.TryGetValue (hash) with
        | true, bucket ->
            bucket.LineDefinitions.Add (lined)
        | _ ->
            let bucket =
                {
                    Triangles = ResizeArray ()
                    TriangleData = ResizeArray ()
                    LineDefinitions = ResizeArray ()
                }
            bucket.LineDefinitions.Add (lined)
            spatialHash.Buckets.Add (hash, bucket)

    let addTriangle (tri: Triangle2D) data spatialHash =
        let size = float spatialHash.CellSize

        let aabb = Triangle2D.aabb tri
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / size) |> int
        let maxY = Math.Floor (float max.Y / size) |> int
        let minX = Math.Floor (float min.X / size) |> int
        let minY = Math.Floor (float min.Y / size) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let min = Vector2 (single (x * spatialHash.CellSize), single (y * spatialHash.CellSize))
                let max = Vector2 (single ((x + 1) * spatialHash.CellSize), single ((y + 1) * spatialHash.CellSize))
                let aabb = AABB2D.ofMinAndMax min max

                if Triangle2D.intersectsAABB aabb tri then
                    let hash = Hash (x, y)
                    addTriangleHash hash tri data spatialHash

    let addLineDefinition (lined: LineDefinition) spatialHash =
        let size = float spatialHash.CellSize

        let aabb = LineSegment2D.aabb lined.LineSegment
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / size) |> int
        let maxY = Math.Floor (float max.Y / size) |> int
        let minX = Math.Floor (float min.X / size) |> int
        let minY = Math.Floor (float min.Y / size) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let min = Vector2 (single (x * spatialHash.CellSize), single (y * spatialHash.CellSize))
                let max = Vector2 (single ((x + 1) * spatialHash.CellSize), single ((y + 1) * spatialHash.CellSize))
                let aabb = AABB2D.ofMinAndMax min max

                if LineSegment2D.intersectsAABB aabb lined.LineSegment then
                    let hash = Hash (x, y)
                    addLineDefinitionHash hash lined spatialHash

    let findWithPoint (p: Vector2) spatialHash =
        let size = float spatialHash.CellSize

        let p0 = Math.Floor (float p.X / size) |> int
        let p1 = Math.Floor (float p.Y / size) |> int

        let hash = Hash (p0, p1)

        let mutable result = Unchecked.defaultof<obj>

        match spatialHash.Buckets.TryGetValue hash with
        | true, bucket ->
            //System.Diagnostics.Debug.WriteLine (String.Format("Triangles Checked: {0}", bucket.Triangles.Count))
            for i = 0 to bucket.TriangleData.Count - 1 do
                if Triangle2D.containsPoint p bucket.Triangles.[i] then
                    result <- bucket.TriangleData.[i]
        | _ -> ()

        result

    let iterWithPoint (p: Vector2) f g spatialHash =
        let size = float spatialHash.CellSize

        let p0 = Math.Floor (float p.X / size) |> int
        let p1 = Math.Floor (float p.Y / size) |> int

        let hash = Hash (p0, p1)

        let mutable result = Unchecked.defaultof<obj>

        match spatialHash.Buckets.TryGetValue hash with
        | true, bucket ->
            for i = 0 to bucket.Triangles.Count - 1 do
                f bucket.Triangles.[i]

            for i = 0 to bucket.LineDefinitions.Count - 1 do
                g bucket.LineDefinitions.[i]

        | _ -> ()
