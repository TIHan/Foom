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

    [<Literal>] 
    let padding = 0.1f

    [<Literal>]
    let maxIterations = 10

    let findIntersectionTime p r q s =
        // p + t r = q + u s
        // u = (q − p) × r / (r × s)
        let qp = (q - p)
        let qpXr = Vec2.perpDot qp r
        let qpXs = Vec2.perpDot qp s
        let rXs = Vec2.perpDot r s


//		if (CmPxr == 0f)
//		{
//			// Lines are collinear, and so intersect if they have any overlap
// 
//			return ((C.X - A.X < 0f) != (C.X - B.X < 0f))
//				|| ((C.Y - A.Y < 0f) != (C.Y - B.Y < 0f));
//		}
// 
//		if (rxs == 0f)
//			return false; // Lines are parallel

        if (qpXr <> 0.f || rXs <> 0.f) then

            if (rXs = 0.f) then 
                1.f
            else

            let u = qpXr / rXs
            let t = qpXs / rXs

            if (t >= 0.f) && (t <= 1.f) && (u >= 0.f) && (u <= 1.f) then
                u
            else
                1.f
        else
            1.f

    let findIntersectionTimeOfPointAndVelocity point velocity seg =
        if LineSegment2D.isPointOnLeftSide point seg then
            1.f
        else

            let p = seg.A
            let r = (seg.B - p)
            let q = point
            let s = velocity
            let result = findIntersectionTime p r q s

            result

    let rec updateAABB (originalVelocity: Vector3) (aabb: AABB2D) (currentPosition: Vector3) (velocity: Vector3) iterations eng =
        if velocity.Equals Vector3.Zero then ()
        elif maxIterations.Equals iterations then ()
        else

        // Let's get 2d versions.
        let originalVelocity2d = Vector2 (originalVelocity.X, originalVelocity.Y)
        let currentPosition2d = Vector2 (currentPosition.X, currentPosition.Y)
        let velocity2d = Vector2 (velocity.X, velocity.Y)
        let velocityReverse2d = -velocity2d

        // Create padded velocity used to nudge a little bit to prevent walking through stuff.
        let paddedVelocity2d = (-velocity2d |> Vector2.Normalize |> (*) padding)

        // Pad the AABB (Axis-Aligned Bounding Box) so its just very very slighly bigger than what was put in.
        let extents = aabb.Extents + Vector2 (padding, padding)
        let aabb = AABB2D.ofCenterAndExtents aabb.Center extents

        let min = aabb.Min ()
        let max = aabb.Max ()

        let v00 = currentPosition2d + Vector2 (min.X, min.Y)
        let v01 = currentPosition2d + Vector2 (max.X, min.Y)
        let v02 = currentPosition2d + Vector2 (max.X, max.Y)
        let v03 = currentPosition2d + Vector2 (min.X, max.Y)

        let e0 = LineSegment2D (v00, v01)
        let e1 = LineSegment2D (v01, v02)
        let e2 = LineSegment2D (v02, v03)
        let e3 = LineSegment2D (v03, v00)

        // Begin collision detection and response for an AABB against walls.

        let mutable hasHitSeg = false
        let mutable hitTime = 1.f
        let mutable segHit = Unchecked.defaultof<LineSegment2D>
        let mutable responseDir = Vector2.Zero

        let aabbTest = 
            (AABB2D.ofCenterAndExtents currentPosition2d extents, AABB2D.ofCenterAndExtents (currentPosition2d + velocity2d) extents) 
            ||> AABB2D.merge

        (aabbTest, eng)
        ||> iterSolidWallByAABB (fun seg ->
            let t =
                [|
                    findIntersectionTimeOfPointAndVelocity v00 velocity2d seg
                    findIntersectionTimeOfPointAndVelocity v01 velocity2d seg
                    findIntersectionTimeOfPointAndVelocity v02 velocity2d seg
                    findIntersectionTimeOfPointAndVelocity v03 velocity2d seg
                |]
                |> Array.min

            let t = 1.f
               // if t = 1.f then
               //     [|
               //         findIntersectionTimeOfPointAndVelocity seg.A velocityReverse2d e0
               //         findIntersectionTimeOfPointAndVelocity seg.B velocityReverse2d e0
               //         findIntersectionTimeOfPointAndVelocity seg.A velocityReverse2d e1
               //         findIntersectionTimeOfPointAndVelocity seg.B velocityReverse2d e1
               //         findIntersectionTimeOfPointAndVelocity seg.A velocityReverse2d e2
               //         findIntersectionTimeOfPointAndVelocity seg.B velocityReverse2d e2
               //         findIntersectionTimeOfPointAndVelocity seg.A velocityReverse2d e3
               //         findIntersectionTimeOfPointAndVelocity seg.B velocityReverse2d e3
               //     |]
               //     |> Array.min
               //else t
                
            if (t - Single.Epsilon > 0.f && t + Single.Epsilon < 1.f) then
                if (t < hitTime) then
                    hitTime <- t
        )


        ()

    let rec moveRigidBodyf directionalVelocity iterations (velocity: Vector2) (z: float32) (rBody: RigidBody) eng =
        if velocity = Vector2.Zero then
            ()
        elif maxIterations = iterations then
            ()
        else
        
        let paddedVelocity = (velocity |> Vector2.Normalize |> (*) -padding)

        match rBody.Shape with
        | DynamicAABB dAABB ->

            let currentPos = rBody.WorldPosition
            let pos2d = currentPos + velocity
            let extents = dAABB.AABB.Extents + Vector2 (padding, padding)
            let shapeAABB = AABB2D.ofCenterAndExtents dAABB.AABB.Center extents
            let aabb = (AABB2D.ofCenterAndExtents rBody.WorldPosition extents, AABB2D.ofCenterAndExtents pos2d extents) ||> AABB2D.merge

            let min = shapeAABB.Min ()
            let max = shapeAABB.Max ()

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
                narrowPhase.Add seg
            )

            let mutable hasPenalty = false
            let mutable hitTime = 1.f
            let mutable firstSegHit : LineSegment2D option = None
            let mutable firstOrigSegHit : LineSegment2D option = None
            let mutable firstPointHit : Vector2 option = None

            // TODO: Implement solver.
            narrowPhase
            |> Seq.distinct
            |> Seq.iter (fun seg ->

                let v00 = rBody.WorldPosition + Vector2 (min.X, min.Y)
                let v01 = rBody.WorldPosition + Vector2 (max.X, min.Y)
                let v02 = rBody.WorldPosition + Vector2 (max.X, max.Y)
                let v03 = rBody.WorldPosition + Vector2 (min.X, max.Y)

                let check point (seg: LineSegment2D) =
                    if LineSegment2D.isPointOnLeftSide point seg then
                        seg, point, 1.f, false
                    else

                    let p = seg.A
                    let r = (seg.B - p)
                    let q = point
                    let s = velocity
                    let result = findIntersectionTime p r q s

                    seg, point, result, false

                let e0 = LineSegment2D (v00, v01)
                let e1 = LineSegment2D (v01, v02)
                let e2 = LineSegment2D (v02, v03)
                let e3 = LineSegment2D (v03, v00)

                let checkRev point (seg: LineSegment2D) =
                    if LineSegment2D.isPointOnLeftSide point seg then
                        seg, point, 1.f, true
                    else

                    let p = seg.A
                    let r = (seg.B - p)
                    let q = point
                    let s = -velocity
                    let result = findIntersectionTime p r q s

                    seg, point, result, true

                let findShortestHitTime () =
                    [
                        check v00 seg
                        check v01 seg
                        check v02 seg
                        check v03 seg
                        checkRev seg.A e0
                        checkRev seg.B e0
                        checkRev seg.A e1
                        checkRev seg.B e1
                        checkRev seg.A e2
                        checkRev seg.B e2
                        checkRev seg.A e3
                        checkRev seg.B e3
                    ]
                    |> List.minBy (fun (_, _, t, _) -> t)

                let origSeg = seg
                let seg, point, u, penalty = findShortestHitTime ()

                let newHitTime = u

                if (newHitTime - Single.Epsilon > 0.f && newHitTime + Single.Epsilon < 1.f) then
                    if newHitTime < hitTime || (newHitTime = hitTime && not penalty)  then

                        hasPenalty <- penalty
                        hitTime <- newHitTime
                        firstSegHit <- Some seg
                        firstOrigSegHit <- Some origSeg
                        firstPointHit <- Some point
                        
            )

            match firstSegHit, firstPointHit with
            | Some seg, Some point ->

                let segDir = (seg.B - seg.A) |> Vector2.Normalize
                let newVelocity = velocity * (hitTime) + paddedVelocity

                warpRigidBody (Vector3 (rBody.WorldPosition + newVelocity, z)) rBody eng

                let wallVelocity = 
                    let dot1 = Vector2.Dot (directionalVelocity, segDir)
                    dot1 * segDir

                moveRigidBodyf directionalVelocity (iterations + 1) wallVelocity z rBody eng

            | _ -> warpRigidBody (Vector3 (rBody.WorldPosition + velocity, z)) rBody eng
        | _ -> ()

    let moveRigidBody (position: Vector3) (rBody: RigidBody) eng =
        let pos = Vector2 (position.X, position.Y)
        let velocity = (pos - rBody.WorldPosition)
        moveRigidBodyf velocity 0 velocity position.Z rBody eng