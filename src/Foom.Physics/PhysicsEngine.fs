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

type PhysicsData =
    {
        StaticWalls: StaticWallSOA
        Triangles: ResizeArray<Triangle2D>
        TriangleData: ResizeArray<obj>

        RigidBodies: Dictionary<int, RigidBody>
    }

    static member Create () =
        {
            StaticWalls = StaticWallSOA.Create ()
            Triangles = ResizeArray ()
            TriangleData = ResizeArray ()
            RigidBodies = Dictionary ()
        }

type PhysicsEngine =
    {
        mutable nextId: int
        spatialHash: SpatialHash2D<PhysicsData>
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicsEngine =

    let create cellSize =
        {
            nextId = 0
            spatialHash = 
                SpatialHash2D<PhysicsData>.Create 
                    cellSize 

                    // ctorData
                    PhysicsData.Create

                    // findByPoint
                    (fun data p ->
                        let mutable result = Unchecked.defaultof<obj>

                        for i = 0 to data.Triangles.Count - 1 do
                            if Triangle2D.containsPoint p data.Triangles.[i] then
                                result <- data.TriangleData.[i]

                        result
                    )
        }

    let warpRigidBody (position: Vector3) (rbody: RigidBody) eng =
        rbody.Hashes
        |> Seq.iter (fun hash ->
            match eng.spatialHash.TryFindDataByHash (Hash2D (hash.X, hash.Y)) with
            | Some data ->

                match rbody.Shape with
                | StaticWall _ ->
                    let index =
                        // Not efficient, but works and shouldn't do anyway.
                        data.StaticWalls.RigidBody.IndexOf (rbody)

                    if (index <> -1) then
                        data.StaticWalls.LineSegment.RemoveAt (index)
                        data.StaticWalls.Id.RemoveAt (index)
                        data.StaticWalls.IsTrigger.RemoveAt (index)
                        data.StaticWalls.RigidBody.RemoveAt (index)
                | _ ->
                    // Anything non-static
                    data.RigidBodies.Remove rbody.Id |> ignore

            | _ -> ()
        )

        rbody.Hashes.Clear ()

        let pos2D = Vector2 (position.X, position.Y)

        let aabb = rbody.AABB

        let min = aabb.Min () + pos2D
        let max = aabb.Max () + pos2D

        let aabb = AABB2D.ofMinAndMax min max

        aabb
        |> eng.spatialHash.AddByAABB (fun hash data ->

            match rbody.Shape with
            | StaticWall staticWall ->
                data.StaticWalls.LineSegment.Add (staticWall.LineSegment)
                data.StaticWalls.Id.Add (rbody.Id)
                data.StaticWalls.IsTrigger.Add (staticWall.IsTrigger)
                data.StaticWalls.RigidBody.Add (rbody)
                rbody.Hashes.Add (Hash (hash.X, hash.Y)) |> ignore

            | _ ->
                data.RigidBodies.Add (rbody.Id, rbody) |> ignore
                rbody.Hashes.Add (Hash (hash.X, hash.Y)) |> ignore

        )

        rbody.WorldPosition <- pos2D
        rbody.Z <- position.Z

    let addRigidBody (rBody: RigidBody) eng =
        rBody.Id <- eng.nextId
        eng.nextId <- eng.nextId + 1

        // This call will eventually be a queue.
        let v = Vector3 (rBody.WorldPosition, rBody.Z)
        warpRigidBody v rBody eng

    let addTriangle (tri: Triangle2D) (o: obj) eng =
        tri
        |> eng.spatialHash.AddByTriangle (fun _ data ->
            data.Triangles.Add tri
            data.TriangleData.Add o
        )

    let findWithPoint p eng =
        eng.spatialHash.FindByPoint p

    let iterSegment f (aabb: AABB2D) eng =
        aabb |> eng.spatialHash.ForEachByAABB (fun physicsData ->
            for i = 0 to physicsData.StaticWalls.LineSegment.Count - 1 do
                let isTrigger = physicsData.StaticWalls.IsTrigger.[i]
                if not isTrigger then
                    f physicsData.StaticWalls.LineSegment.[i]
        )

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
            ||> iterSegment (fun seg ->
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

                let e0 = { A = v00; B = v01 }
                let e1 = { A = v01; B = v02 }
                let e2 = { A = v02; B = v03 }
                let e3 = { A = v03; B = v00 }

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