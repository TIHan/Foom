[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System
open System.Numerics

open Foom.Wad.Geometry

[<Struct>]
type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new (x, y, z) = { X = x; Y = y; Z = z }

type Ray =
    {
        Origin: Vector2
        Direction: Vector2
    }

    member this.GetPoint (distance: float32) =
        this.Origin + (this.Direction * distance)

let inline isReflexVertex (prev: Vector2) (next: Vector2) (vertex: Vector2) =
    let p1 = prev - vertex
    let p2 = next - vertex
    Vector3.Cross(Vector3 (p1.X, p1.Y, 0.f), Vector3 (p2.X, p2.Y, 0.f)).Z < 0.f

//public static bool RayIntersectsSegment(Ray ray, Vector2 pt0, Vector2 pt1, float tmax, out float t) {
//    Vector2 seg = pt1 - pt0;
//    Vector2 segPerp = LeftPerp(seg);
//    float perpDotd = Vector2.Dot(ray.Direction, segPerp);
//    if (Equals(perpDotd, 0.0f, float.Epsilon))
//    {
//        t = float.MaxValue;
//        return false;
//    }

//    Vector2 d = pt0 - ray.Origin;

//    t = Vector2.Dot(segPerp, d) / perpDotd;
//    float s = Vector2.Dot(LeftPerp(ray.Direction), d) / perpDotd;

//    return t >= 0.0f && t <= tmax && s >= 0.0f && s <= 1.0f;
//}

let inline equals value1 value2 = abs (value1 - value2) < System.Single.Epsilon

let inline leftPerp (v: Vector2) = Vector2 (v.Y, -v.X)

let inline rightPerp (v: Vector2) = Vector2 (-v.Y, v.X)

//http://afloatingpoint.blogspot.com/2011/04/2d-polygon-raycasting.html
let rayIntersectsSegment (ray: Ray) (pt0: Vector2) (pt1: Vector2) (tmax: float32) (t: float32 byref) =
    let seg = pt1 - pt0
    let segPerp = leftPerp (seg)
    let perpDotd = Vector2.Dot (ray.Direction, segPerp)

    if equals perpDotd 0.0f then
        t <- System.Single.MaxValue
        false
    else
        let d = pt0 - ray.Origin

        t <- Vector2.Dot (segPerp, d) / perpDotd
        let s = Vector2.Dot (leftPerp (ray.Direction), d) / perpDotd

        t >= 0.0f && t <= tmax && s >= 0.0f && s <= 1.0f


//public static bool RayCast(Ray ray, Polygon polygon, float tmax, out float t, out Vector2 pt, out Vector2 normal)
//        {
//            t = float.MaxValue;
//            pt = ray.Origin;
//            normal = ray.Direction;
            
//            // temp holder for segment distance
//            float distance;
//            int crossings = 0;

//            for (int j = polygon.NumVertices - 1, i = 0; i < polygon.NumVertices; j = i, i++)
//            {
//                if (RayIntersectsSegment(ray, polygon.v[j], polygon.v[i], float.MaxValue, out distance))
//                {
//                    crossings++;
//                    if (distance < t && distance <= tmax)
//                    {
//                        t = distance;
//                        pt = ray.GetPoint(t);

//                        Vector2 edge = polygon.v[j] - polygon.v[i];
//                        // We would use LeftPerp() if the polygon was
//                        // in clock wise order
//                        normal = Vector2.Normalize(RightPerp(edge));
//                    }
//                }
//            }
//            return crossings > 0 && crossings % 2 == 0;
//        }

let rayCast (ray: Ray) (vertices: Vector2 []) (tmax: float32) (t: float32 byref) (pt: Vector2 byref) (normal: Vector2 byref) =
    t <- Single.MaxValue
    pt <- ray.Origin
    normal <- ray.Direction
    
    // temp holder for segment distance
    let mutable distance = 0.f
    let mutable crossings = 0

    let mutable j = vertices.Length - 1
    let mutable i = 0

    let mutable edge = (-1, -1)

    while (i < vertices.Length || crossings <= 0) do

        if (rayIntersectsSegment ray vertices.[j] vertices.[i] Single.MaxValue &distance) then
            crossings <- crossings + 1
            edge <- (j, i)
            if (distance < t && distance <= tmax) then
                t <- distance
                pt <- ray.GetPoint(t)

                let edge = vertices.[j] - vertices.[i]

                // We would use LeftPerp() if the polygon was
                // in clock wise order
                normal <- Vector2.Normalize( leftPerp (edge))

        j <- i
        i <- i + 1

    if crossings > 0 then
        Some (edge)
    else
        None


let inline pointInsideTriangle p v =
    Polygon.isPointInside p (Polygon.create v)


let computeVertices (vertices: Vector2 seq) f =

    let rec computeVertices (recursiveSteps: int) (vertices: Vector2 ResizeArray) currentIndex = 
        if recursiveSteps > vertices.Count then
            //failwith "Unable to triangulate"
            ()
        else

        if vertices.Count < 3 then
            ()
        elif vertices.Count = 3 then
            f vertices.[2] vertices.[1] vertices.[0]
        else

        let pPrev =
            if currentIndex = 0 then
                vertices.[vertices.Count - 1]
            else
                vertices.[currentIndex - 1]

        let pCur = vertices.[currentIndex]

        let pNext =
            if currentIndex = (vertices.Count - 1) then
                vertices.[0]
            else
                vertices.[currentIndex + 1]

        let triangle = [|pNext;pCur;pPrev|]

        let anyPointsInsideTriangle =
            vertices
            |> Seq.exists (fun x ->
                (x <> pPrev) && (x <> pCur) && (x <> pNext) && pointInsideTriangle x triangle
            )

        if isReflexVertex pPrev pNext pCur || anyPointsInsideTriangle then
            let nextIndex =
                if currentIndex >= (vertices.Count - 1) then
                    0
                else
                    currentIndex + 1
            computeVertices (recursiveSteps + 1) (vertices) nextIndex
        else
            vertices.RemoveAt(currentIndex)

            let nextIndex =
                if currentIndex >= (vertices.Count - 1) then
                    0
                else
                    currentIndex + 1

            f pNext pCur pPrev
            computeVertices 0 vertices nextIndex

    computeVertices 0 (ResizeArray (vertices)) 0

let compute polygon =

    let triangles = ResizeArray<Triangle2D> ()

    computeVertices (Polygon.vertices polygon) (fun x y z ->
        triangles.Add (Triangle2D (x, y, z))
    )

    if triangles.Count = 0 then
        None
    else
        let result = 
            triangles
            |> Seq.toArray

        Some result

let computeTree (tree: PolygonTree) =

    if tree.Children.IsEmpty then
        match compute tree.Polygon with
        | None -> [ ]
        | Some triangles ->
            [ triangles ]
    else

        let mutable vertices = tree.Polygon |> Polygon.vertices
        let mutable result = Array.empty<Vector2>

        tree.Children
        |> List.iteri (fun i childTree ->

            if childTree.Children.Length > 0 then
                failwith "butt"

            if i = 1 || i = 2 then
                let childVertices = childTree.Polygon |> Polygon.vertices

                let childMax = childVertices |> Array.maxBy (fun x -> x.X)

                let ray = { Origin = childMax; Direction = Vector2.UnitX }

                let mutable t = 0.f
                let mutable pt = Vector2.Zero
                let mutable normal = Vector2.Zero

                match (rayCast ray vertices Single.MaxValue &t &pt &normal) with
                | Some (edge1Index, edge2Index) ->

                    let childMaxIndex = childVertices |> Array.findIndex (fun x -> x = childMax)

                    if (vertices |> Array.exists (fun x -> pointInsideTriangle x [|childVertices.[childMaxIndex];vertices.[edge2Index];pt|])) then
                        failwith "butt"

                    let childMaxIndex = childVertices |> Array.findIndex (fun x -> x = childMax)

                    let linkedList = vertices |> System.Collections.Generic.List



                    let mutable i = childMaxIndex
                    let mutable count = 0
                    let linkedList2 = System.Collections.Generic.List ()

                    linkedList2.Add(pt)
                    while (count < childVertices.Length) do
                        linkedList2.Add(childVertices.[i])
                        i <-
                            if i + 1 >= childVertices.Length then
                                0
                            else
                                i + 1
                        count <- count + 1

                    //linkedList2.Add(linkedList2.[0])
                    linkedList2.Add(childVertices.[childMaxIndex])
                    linkedList2.Add(pt)



                    linkedList.InsertRange(edge2Index, linkedList2)

                    vertices <- (linkedList |> Seq.toArray)
                    result <- 
                        match compute (Polygon.create vertices) with
                        | None -> vertices
                        | Some triangles ->
                            triangles
                            |> Array.map (fun x -> [|x.X;x.Y;x.Z|])
                            |> Array.reduce Array.append

                | _ -> ()
        )   

        let triangles = ResizeArray<Triangle2D> ()
        let mutable i = 0

        while (i < result.Length) do
            Triangle2D (
                result.[i],
                result.[i + 1],
                result.[i + 2]
            )
            |> triangles.Add
            i <- i + 3

        [
            triangles
            |> Seq.toArray
        ]
