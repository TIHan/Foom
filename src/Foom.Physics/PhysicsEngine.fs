namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

[<Struct>]
type Hash =

    val X : int

    val Y : int

    new (x, y) = { X = x; Y = y }

[<ReferenceEquality>]
type DynamicCircle =
    {
        mutable Circle: Circle2D
        mutable Z: float32
        mutable Height: float32
        Hashes: HashSet<Hash>
    }

type StaticLine =
    {
        FrontFaceAreaId: int
        BackFaceAreaId: int

        LineSegment: LineSegment2D
        IsWall: bool
    }

type SpatialHashBucket =
    {
        AABB: AABB2D

        Triangles: ResizeArray<Triangle2D>
        TriangleData: ResizeArray<obj>

        StaticLines: ResizeArray<StaticLine>
        DynamicCircles: HashSet<DynamicCircle>
    }

type PhysicsEngine =
    {
        CellSize: int
        CellSizeDouble: double
        Buckets: Dictionary<Hash, SpatialHashBucket>
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicsEngine =

    let create cellSize =
        {
            CellSize = cellSize
            CellSizeDouble = float cellSize
            Buckets = Dictionary ()
        }

    let addTriangleHash hash tri data eng =
        match eng.Buckets.TryGetValue (hash) with
        | true, bucket ->
            bucket.Triangles.Add (tri)
            bucket.TriangleData.Add (data)
        | _ ->
            let min = Vector2 (single (hash.X * eng.CellSize), single (hash.Y * eng.CellSize))
            let max = Vector2 (single ((hash.X + 1) * eng.CellSize), single ((hash.Y + 1) * eng.CellSize))
            let aabb = AABB2D.ofMinAndMax min max
            let bucket =
                {
                    AABB = aabb
                    Triangles = ResizeArray ()
                    TriangleData = ResizeArray ()
                    StaticLines = ResizeArray ()
                    DynamicCircles = HashSet ()
                }
            bucket.Triangles.Add (tri)
            bucket.TriangleData.Add (data)
            eng.Buckets.Add (hash, bucket)

    let addStaticLineHash hash lined eng =
        match eng.Buckets.TryGetValue (hash) with
        | true, bucket ->
            bucket.StaticLines.Add (lined)
        | _ ->
            let min = Vector2 (single (hash.X * eng.CellSize), single (hash.Y * eng.CellSize))
            let max = Vector2 (single ((hash.X + 1) * eng.CellSize), single ((hash.Y + 1) * eng.CellSize))
            let aabb = AABB2D.ofMinAndMax min max
            let bucket =
                {
                    AABB = aabb
                    Triangles = ResizeArray ()
                    TriangleData = ResizeArray ()
                    StaticLines = ResizeArray ()
                    DynamicCircles = HashSet ()
                }
            bucket.StaticLines.Add (lined)
            eng.Buckets.Add (hash, bucket)

    let addTriangle (tri: Triangle2D) data eng =
        let aabb = Triangle2D.aabb tri
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / eng.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / eng.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / eng.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / eng.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let min = Vector2 (single (x * eng.CellSize), single (y * eng.CellSize))
                let max = Vector2 (single ((x + 1) * eng.CellSize), single ((y + 1) * eng.CellSize))
                let aabb = AABB2D.ofMinAndMax min max

                if Triangle2D.intersectsAABB aabb tri then
                    let hash = Hash (x, y)
                    addTriangleHash hash tri data eng

    let addStaticLine (lined: StaticLine) eng =
        let aabb = LineSegment2D.aabb lined.LineSegment
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / eng.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / eng.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / eng.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / eng.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let min = Vector2 (single (x * eng.CellSize), single (y * eng.CellSize))
                let max = Vector2 (single ((x + 1) * eng.CellSize), single ((y + 1) * eng.CellSize))
                let aabb = AABB2D.ofMinAndMax min max

                if LineSegment2D.intersectsAABB aabb lined.LineSegment then
                    let hash = Hash (x, y)
                    addStaticLineHash hash lined eng

    let warpDynamicCircle (position: Vector3) (dCircle: DynamicCircle) eng =
        dCircle.Hashes
        |> Seq.iter (fun hash ->
            eng.Buckets.[hash].DynamicCircles.Remove dCircle |> ignore
        )
        dCircle.Hashes.Clear ()

        let radius = dCircle.Circle.Radius
        dCircle.Circle.Center <- Vector2 (position.X, position.Y)

        let minX = Math.Floor (float (position.X - radius) / eng.CellSizeDouble) |> int
        let maxX = Math.Floor (float (position.X + radius) / eng.CellSizeDouble) |> int
        let minY = Math.Floor (float (position.Y - radius) / eng.CellSizeDouble) |> int
        let maxY = Math.Floor (float (position.Y + radius) / eng.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let hash = Hash (x, y)

                match eng.Buckets.TryGetValue (hash) with
                | true, bucket ->
                    bucket.DynamicCircles.Add dCircle |> ignore
                    dCircle.Hashes.Add (hash) |> ignore
                | _ -> ()

    // The grand daddy. This needs thorough research.
    // Goal: The circle will not pass through lines marked as walls.
    let moveDynamicCircle (position: Vector3) dCircle eng =
        let currentPosition = dCircle.Circle.Center
        let radius = dCircle.Circle.Radius

        let seg = LineSegment2D (Vector2 (currentPosition.X, currentPosition.Y), Vector2 (position.X, position.Y))

        let minX = Math.Floor (float (currentPosition.X - radius) / eng.CellSizeDouble) |> int
        let maxX = Math.Floor (float (currentPosition.X + radius) / eng.CellSizeDouble) |> int
        let minY = Math.Floor (float (currentPosition.Y - radius) / eng.CellSizeDouble) |> int
        let maxY = Math.Floor (float (currentPosition.Y + radius) / eng.CellSizeDouble) |> int

        let mutable position = position
        for x = minX to maxX do
            for y = minY to maxY do

            let newMinX = Math.Floor (float (position.X - radius) / eng.CellSizeDouble) |> int
            let newMaxX = Math.Floor (float (position.X + radius) / eng.CellSizeDouble) |> int
            let newMinY = Math.Floor (float (position.Y - radius) / eng.CellSizeDouble) |> int
            let newMaxY = Math.Floor (float (position.Y + radius) / eng.CellSizeDouble) |> int

            let hash = Hash (x, y)

            match eng.Buckets.TryGetValue (hash) with
            | true, bucket ->
                ()
            | _ -> ()


    let findWithPoint (p: Vector2) eng =
        let p0 = Math.Floor (float p.X / eng.CellSizeDouble) |> int
        let p1 = Math.Floor (float p.Y / eng.CellSizeDouble) |> int

        let hash = Hash (p0, p1)

        let mutable result = Unchecked.defaultof<obj>

        match eng.Buckets.TryGetValue hash with
        | true, bucket ->
            //System.Diagnostics.Debug.WriteLine (String.Format("Triangles Checked: {0}", bucket.Triangles.Count))
            for i = 0 to bucket.TriangleData.Count - 1 do
                if Triangle2D.containsPoint p bucket.Triangles.[i] then
                    result <- bucket.TriangleData.[i]
        | _ -> ()

        result

    let iterWithPoint (p: Vector2) f g eng =
        let p0 = Math.Floor (float p.X / eng.CellSizeDouble) |> int
        let p1 = Math.Floor (float p.Y / eng.CellSizeDouble) |> int

        let hash = Hash (p0, p1)

        let mutable result = Unchecked.defaultof<obj>

        match eng.Buckets.TryGetValue hash with
        | true, bucket ->
            for i = 0 to bucket.Triangles.Count - 1 do
                f bucket.Triangles.[i]

            for i = 0 to bucket.StaticLines.Count - 1 do
                g bucket.StaticLines.[i]

        | _ -> ()

    let debugFindSpacesByDynamicCircle dCircle eng =
        dCircle.Hashes
        |> Seq.map (fun hash -> eng.Buckets.[hash].AABB)
