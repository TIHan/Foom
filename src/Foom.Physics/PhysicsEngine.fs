namespace Foom.Physics

open System
open System.Numerics
open System.Collections.Generic

open Foom.Math
open Foom.Geometry

type StaticWallSOA =
    {
        LineSegment: ResizeArray<LineSegment2D>
        Id: ResizeArray<int>
        IsTrigger: ResizeArray<bool>
        RigidBody: ResizeArray<RigidBody>
    }

    static member Create () =
        {
            LineSegment = ResizeArray ()
            Id = ResizeArray ()
            IsTrigger = ResizeArray ()
            RigidBody = ResizeArray ()
        }

type SpatialHashBucket =
    {
        AABB: AABB2D
        StaticWalls: StaticWallSOA

        Triangles: ResizeArray<Triangle2D>
        TriangleData: ResizeArray<obj>

        RigidBodies: Dictionary<int, RigidBody>
    }

    static member Create (aabb) =
        {
            AABB = aabb
            StaticWalls = StaticWallSOA.Create () 
            Triangles = ResizeArray ()
            TriangleData = ResizeArray ()
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
            match eng.Buckets.TryGetValue hash with
            | true, bucket ->

                match rbody.Shape with
                | StaticWall _ ->
                    let index =
                        // Not efficient, but works and shouldn't do anyway.
                        bucket.StaticWalls.RigidBody.IndexOf (rbody)

                    if (index <> -1) then
                        bucket.StaticWalls.LineSegment.RemoveAt (index)
                        bucket.StaticWalls.Id.RemoveAt (index)
                        bucket.StaticWalls.IsTrigger.RemoveAt (index)
                        bucket.StaticWalls.RigidBody.RemoveAt (index)
                | _ ->
                    // Anything non-static
                    bucket.RigidBodies.Remove rbody.Id |> ignore

            | _ -> ()
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

                match rbody.Shape with
                | StaticWall staticWall ->
                    let bucket =
                        match eng.Buckets.TryGetValue hash with
                        | false, _ ->
                            let bucket = SpatialHashBucket.Create aabb
                            eng.Buckets.Add (hash, bucket)
                            bucket
                        | _, bucket -> bucket

                    bucket.StaticWalls.LineSegment.Add (staticWall.LineSegment)
                    bucket.StaticWalls.Id.Add (rbody.Id)
                    bucket.StaticWalls.IsTrigger.Add (staticWall.IsTrigger)
                    bucket.StaticWalls.RigidBody.Add (rbody)
                    rbody.Hashes.Add hash |> ignore

                | _ ->
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

            for i = 0 to bucket.StaticWalls.LineSegment.Count - 1 do
                g bucket.StaticWalls.LineSegment.[i]

        | _ -> ()

    let debugFindSpacesByRigidBody (rbody: RigidBody) eng =
        rbody.Hashes
        |> Seq.map (fun hash -> eng.Buckets.[hash].AABB)

    let iterSolidWallByAABB f (aabb: AABB2D) eng =
        let min = aabb.Min ()
        let max = aabb.Max ()

        let maxX = Math.Floor (float max.X / eng.CellSizeDouble) |> int
        let maxY = Math.Floor (float max.Y / eng.CellSizeDouble) |> int
        let minX = Math.Floor (float min.X / eng.CellSizeDouble) |> int
        let minY = Math.Floor (float min.Y / eng.CellSizeDouble) |> int

        for x = minX to maxX do
            for y = minY to maxY do

                let hash = Hash (x, y)

                match eng.Buckets.TryGetValue hash with
                | true, bucket ->

                    bucket.StaticWalls.LineSegment
                    |> Seq.iteri (fun i seg ->
                        if not bucket.StaticWalls.IsTrigger.[i] then
                            f seg
                    )

                | _ -> ()


    let moveRigidBody (position: Vector3) (rBody: RigidBody) eng =
        if Vector3 (rBody.WorldPosition, rBody.Z) = position then ()
        else
        
        match rBody.Shape with
        | DynamicAABB dAABB ->

            let mutable pos2d = Vector2 (position.X, position.Y)
            let aabb = (AABB2D.ofCenterAndExtents rBody.WorldPosition dAABB.AABB.Extents, AABB2D.ofCenterAndExtents pos2d dAABB.AABB.Extents) ||> AABB2D.merge

            let min = dAABB.AABB.Min ()
            let max = dAABB.AABB.Max ()

            let broadPhase = ResizeArray ()

            // Broad
            (aabb, eng)
            ||> iterSolidWallByAABB (fun seg ->
                broadPhase.Add (seg)
            )

            let narrowPhase = ResizeArray ()

            // Narrow
            broadPhase
            |> Seq.iter (fun seg ->

                if LineSegment2D.isPointOnLeftSide rBody.WorldPosition seg |> not then
                    narrowPhase.Add seg
            )

            // TODO: Implement solver.

            warpRigidBody (Vector3 (pos2d, position.Z)) rBody eng

        | _ -> ()
