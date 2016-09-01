namespace Foom.DataStructures

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

// TODO: Implement SpatialHash2D

type SpatialHashBucket<'T> =
    {
        Triangles: ResizeArray<Triangle2D>
        TriangleData: ResizeArray<'T>
    }

[<Struct>]
type Hash =

    val X : int

    val Y : int

    new (x, y) = { X = x; Y = y }

type SpatialHash2D<'T> =
    {
        CellSize: int
        Buckets: Dictionary<Hash, SpatialHashBucket<'T>>
    }

module SpatialHash2D =

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
                }
            bucket.Triangles.Add (tri)
            bucket.TriangleData.Add (data)
            spatialHash.Buckets.Add (hash, bucket)

    // FIXME: This is wrong. Let's try a AABB.
    let addStaticTriangle (tri: Triangle2D) data spatialHash =
        let size = float spatialHash.CellSize

        let aabb = Triangle2D.aabb tri
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / size) |> int
        let maxY = Math.Floor (float max.Y / size) |> int
        let minX = Math.Floor (float min.X / size) |> int
        let minY = Math.Floor (float min.Y / size)|> int

        for x = minX to maxX do
            for y = minY to maxY do
                let hash = Hash (x, y)
                addTriangleHash hash tri data spatialHash

    let queryWithPoint (p: Vector2) f spatialHash =
        let size = float spatialHash.CellSize

        let p0 = Math.Floor (float p.X / size) |> int
        let p1 = Math.Floor (float p.Y / size) |> int

        let hash = Hash (p0, p1)

        match spatialHash.Buckets.TryGetValue hash with
        | true, bucket ->
           // System.Diagnostics.Debug.WriteLine (String.Format("Triangles Checked: {0}", bucket.Triangles.Count))
            for i = 0 to bucket.TriangleData.Count - 1 do
                if Triangle2D.containsPoint p bucket.Triangles.[i] then
                    f bucket.TriangleData.[i]
        | _ -> ()
