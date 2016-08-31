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

type SpatialHash2D<'T> =
    {
        CellSize: int
        Buckets: Dictionary<int, SpatialHashBucket<'T>>
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

        let a0 = Math.Floor (float tri.A.X / size) |> int
        let a1 = Math.Floor (float tri.A.Y / size) |> int
        let b0 = Math.Floor (float tri.B.X / size) |> int
        let b1 = Math.Floor (float tri.B.Y / size) |> int
        let c0 = Math.Floor (float tri.C.X / size) |> int
        let c1 = Math.Floor (float tri.C.Y / size) |> int

        addTriangleHash (a0 + a1) tri data spatialHash
        addTriangleHash (b0 + b1) tri data spatialHash
        addTriangleHash (c0 + c1) tri data spatialHash

    let queryWithPoint (p: Vector2) f spatialHash =
        let size = float spatialHash.CellSize

        let p0 = Math.Floor (float p.X / size) |> int
        let p1 = Math.Floor (float p.Y / size) |> int

        match spatialHash.Buckets.TryGetValue (p0 + p1) with
        | true, bucket ->
            for i = 0 to bucket.TriangleData.Count - 1 do
                let tri = bucket.Triangles.[i]
                if Triangle2D.containsPoint p tri then
                    f bucket.TriangleData.[i]
        | _ -> ()
