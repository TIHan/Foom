namespace Foom.Geometry

open System
open System.Numerics
open System.Collections.Generic

[<Struct>]
type Hash2D =

    val X : int

    val Y : int

    new (x, y) = { X = x; Y = y }

type SpatialHash2DBucket<'T> =
    {
        AABB: AABB2D
        Data: 'T
    }

    static member Create (aabb, data) =
        {
            AABB = aabb
            Data = data
        }

type SpatialHash2D<'T> =
    {
        CellSize: int
        CellSizeDouble: double
        Buckets: Dictionary<Hash2D, SpatialHash2DBucket<'T>>

        ctorData: (unit -> 'T)
        findByPoint: 'T -> Vector2 -> obj
    }

    static member Create cellSize ctorData findbyPoint =
        {
            CellSize = cellSize
            CellSizeDouble = double cellSize
            Buckets = Dictionary ()
            ctorData = ctorData
            findByPoint = findbyPoint
        }

    member this.InternalAdd<'U> hash f =
        let bucket =
            match this.Buckets.TryGetValue (hash) with
            | true, bucket -> bucket
            | _ ->
                let min = Vector2 (single (hash.X * this.CellSize), single (hash.Y * this.CellSize))
                let max = Vector2 (single ((hash.X + 1) * this.CellSize), single ((hash.Y + 1) * this.CellSize))
                let aabb = AABB2D.ofMinAndMax min max
                let bucket = SpatialHash2DBucket<'T>.Create (aabb, this.ctorData ())

                this.Buckets.Add (hash, bucket)

                bucket

        f hash bucket.Data

    member this.TryFindDataByHash hash =
        match this.Buckets.TryGetValue hash with
        | true, bucket -> Some bucket.Data
        | _ -> None

    member this.AddByTriangle f (tri: Triangle2D) =
        let aabb = tri.BoundingBox ()
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / this.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / this.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / this.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / this.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let min = Vector2 (single (x * this.CellSize), single (y * this.CellSize))
                let max = Vector2 (single ((x + 1) * this.CellSize), single ((y + 1) * this.CellSize))
                let aabb = AABB2D.ofMinAndMax min max

                if tri.Intersects aabb then
                    let hash = Hash2D (x, y)
                    this.InternalAdd hash f

    member this.AddByAABB f (aabb: AABB2D) =
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / this.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / this.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / this.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / this.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let hash = Hash2D (x, y)

                this.InternalAdd hash f


    member this.FindByPoint (p: Vector2) =
        let p0 = Math.Floor (float p.X / this.CellSizeDouble) |> int
        let p1 = Math.Floor (float p.Y / this.CellSizeDouble) |> int

        let hash = Hash2D (p0, p1)

        let mutable result = Unchecked.defaultof<obj>

        match this.Buckets.TryGetValue hash with
        | true, bucket ->
            if obj.ReferenceEquals (result, null) then
                result <- this.findByPoint bucket.Data p
        | _ -> ()

        result

    member this.ForEachByAABB f (aabb: AABB2D) =
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / this.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / this.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / this.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / this.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let hash = Hash2D (x, y)

                match this.Buckets.TryGetValue hash with
                | true, bucket ->

                    f bucket.Data

                | _ -> ()