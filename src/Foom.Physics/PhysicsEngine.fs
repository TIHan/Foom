namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

type SpatialHashBucket =
    {
        AABB: AABB2D

        Triangles: ResizeArray<Triangle2D>
        TriangleData: ResizeArray<obj>

        LineSegments: ResizeArray<LineSegment2D>
        LineSegmentsIsWall: ResizeArray<bool>

        RigidBodies: Dictionary<int, RigidBody>
    }

    static member Create (aabb) =
        {
            AABB = aabb
            Triangles = ResizeArray ()
            TriangleData = ResizeArray ()
            LineSegments = ResizeArray ()
            LineSegmentsIsWall = ResizeArray ()
            RigidBodies = Dictionary ()
        }

type PhysicsEngine =
    {
        mutable NextId: int
        CellSize: int
        CellSizeDouble: double
        Buckets: Dictionary<Hash, SpatialHashBucket>
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicsEngine =

    let create cellSize =
        {
            NextId = 1
            CellSize = cellSize
            CellSizeDouble = float cellSize
            Buckets = Dictionary ()
        }

    let warpRigidBody (position: Vector3) (rbody: RigidBody) eng =
        rbody.Hashes
        |> Seq.iter (fun hash ->
            eng.Buckets.[hash].RigidBodies.Remove rbody.Id |> ignore
        )

        rbody.Hashes.Clear ()

        let pos2D = Vector2 (position.X, position.Y)

        let aabb = rbody.AABB

        let min = aabb.Min () + pos2D
        let max = aabb.Max () + pos2D

        let maxX = Math.Floor (float max.X / eng.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / eng.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / eng.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / eng.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let hash = Hash (x, y)

                match eng.Buckets.TryGetValue hash with
                | true, bucket ->
                    bucket.RigidBodies.Add (rbody.Id, rbody) |> ignore
                    rbody.Hashes.Add hash |> ignore
                | _ -> ()
        
        rbody.WorldPosition <- pos2D
        rbody.Z <- position.Z

    let addRigidBody (rBody: RigidBody) eng =
        rBody.Id <- eng.NextId
        eng.NextId <- eng.NextId + 1

        // This call will eventually be a queue.
        let v = Vector3 (rBody.WorldPosition, rBody.Z)
        warpRigidBody v rBody eng

    let addTriangleHash hash tri data eng =
        match eng.Buckets.TryGetValue (hash) with
        | true, bucket ->
            bucket.Triangles.Add (tri)
            bucket.TriangleData.Add (data)
        | _ ->
            let min = Vector2 (single (hash.X * eng.CellSize), single (hash.Y * eng.CellSize))
            let max = Vector2 (single ((hash.X + 1) * eng.CellSize), single ((hash.Y + 1) * eng.CellSize))
            let aabb = AABB2D.ofMinAndMax min max
            let bucket = SpatialHashBucket.Create aabb
            bucket.Triangles.Add (tri)
            bucket.TriangleData.Add (data)
            eng.Buckets.Add (hash, bucket)

    let addLineSegmentHash hash seg isWall eng =
        match eng.Buckets.TryGetValue (hash) with
        | true, bucket ->
            bucket.LineSegments.Add seg
            bucket.LineSegmentsIsWall.Add isWall
        | _ ->
            let min = Vector2 (single (hash.X * eng.CellSize), single (hash.Y * eng.CellSize))
            let max = Vector2 (single ((hash.X + 1) * eng.CellSize), single ((hash.Y + 1) * eng.CellSize))
            let aabb = AABB2D.ofMinAndMax min max
            let bucket = SpatialHashBucket.Create aabb
            bucket.LineSegments.Add seg
            bucket.LineSegmentsIsWall.Add isWall
            eng.Buckets.Add (hash, bucket)

    let addTriangle (tri: Triangle2D) (data: obj) eng =
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

    let addLineSegment seg isWall eng =
        let aabb = LineSegment2D.aabb seg
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

                if LineSegment2D.intersectsAABB aabb seg then
                    let hash = Hash (x, y)
                    addLineSegmentHash hash seg isWall eng

    let iterLineSegmentByAABB (aabb: AABB2D) f eng =
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / eng.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / eng.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / eng.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / eng.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let hash = Hash (x, y)

                match eng.Buckets.TryGetValue (hash) with
                | true, bucket ->
                    bucket.LineSegments
                    |> Seq.iter f
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

            for i = 0 to bucket.LineSegments.Count - 1 do
                g bucket.LineSegments.[i]

        | _ -> ()

    let debugFindSpacesByRigidBody (rbody: RigidBody) eng =
        rbody.Hashes
        |> Seq.map (fun hash -> eng.Buckets.[hash].AABB)

