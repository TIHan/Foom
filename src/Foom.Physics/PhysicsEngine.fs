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

    let iterStaticLineByAABB (aabb: AABB2D) f eng =
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
                    bucket.StaticLines
                    |> Seq.iter f
                | _ -> ()

    // The grand daddy. This needs thorough research.
    // Goal: The circle will not pass through lines marked as walls.
    // References: http://seb.ly/2010/01/predicting-circle-line-collisions/
    let moveDynamicCircle (position: Vector3) dCircle eng =


        let circleAABB = dCircle.Circle |> Circle2D.aabb

        let mutable newCircle = dCircle.Circle
        newCircle.Center <- Vector2 (position.X, position.Y)
        let newCircleAABB = newCircle |> Circle2D.aabb
        let aabb = AABB2D.merge circleAABB newCircleAABB

        let mutable positionXY = Vector2 (position.X, position.Y)
        let mutable offsetXY = Vector2.Zero
        let radius = dCircle.Circle.Radius
        let arr = ResizeArray ()

        let moveDir = positionXY - dCircle.Circle.Center |> Vector2.Normalize

        let hashLines = HashSet ()
        eng
        |> iterStaticLineByAABB aabb
            (fun sLine ->
                if hashLines.Add (sLine.LineSegment) then
                    if sLine.IsWall && LineSegment2D.isPointOnLeftSide positionXY sLine.LineSegment |> not then 
                        arr.Add (sLine)


            )

        let mutable maxX = 0.f
        let mutable minX = 0.f
        let mutable maxY = 0.f
        let mutable minY = 0.f
        arr
        //|> Seq.sortBy (fun sLine ->
        //    let normal = LineSegment2D.normal sLine.LineSegment

        //    Vector2.Dot (moveDir, normal)
        //)
//We can use this formula :

//current distance = d1 + (d2-d1) * t 
//so when d = the radius r :

//r = d1 + (d2-d1) * t 
//and with some algebra even I can just about manage we can extract t :

//r-d1 = (d2-d1)*t
//(r - d1) / (d2-d1) = t 
//So if t is between 0 and 1 we know we collided between frames!

//Whew. This blog post is getting epic. But we’re not quite there yet, we still need to work out where the circle is at the point of collision: we take the vector between C1 and C2, multiply it by t and add it to C1.
        |> Seq.iter (fun (sLine) ->

                let t, d = sLine.LineSegment |> LineSegment2D.findClosestPointByPoint positionXY
                let diff = positionXY - d
                let len = diff.Length ()
                let dir = diff |> Vector2.Normalize

                let normal = LineSegment2D.normal sLine.LineSegment
                let dp = Vector2.Dot (normal, diff)
                if len <= radius && dp > 0.f then

                    let offset = (normal * (radius - len))

                    if offset.X > 0.f && maxX < offset.X then
                        maxX <- offset.X

                    if offset.X < 0.f && minX > offset.X then
                        minX <- offset.X

                    if offset.Y > 0.f && maxY < offset.Y then
                        maxY <- offset.Y

                    if offset.Y < 0.f && minY > offset.Y then
                        minY <- offset.Y
        )

        let mutable offsetXY = Vector2 (maxX + minX, maxY + minY)

       // System.Diagnostics.Debug.WriteLine (String.Format ("{0} {1} {2} {3}", maxX, minX, maxY, minY))

        //|> Array.ofSeq
        //|> Array.toSeq
        //|> Seq.iter (fun offset ->
        //    if offsetXY.X < offset.X then
        //        offsetXY.X <- offset.X

        //    if offsetXY.Y < offset.Y then
        //       offsetXY.Y <- offset.Y
        //)

        warpDynamicCircle (Vector3 (positionXY + offsetXY, 0.f)) dCircle eng

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
