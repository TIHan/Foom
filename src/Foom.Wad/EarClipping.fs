﻿[<RequireQualifiedAccess>]
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

let inline perpDot (left: Vector2) (right: Vector2) = left.X * right.Y - left.Y * right.X

let inline isPointOnLeftSide (v1: Vector2) (v2: Vector2) (p: Vector2) =
    (v2.X - v1.X) * (p.Y - v1.Y) - (v2.Y - v1.Y) * (p.X - v1.X) > 0.f

// https://rootllama.wordpress.com/2014/06/20/ray-line-segment-intersection-test-in-2d/
let rayIntersection (ray: Ray) (vertices: Vector2 []) =
    let mutable i = vertices.Length - 1
    let mutable j = 0

    let segmentsHit = ResizeArray<int * int * Vector2> ()

    let testSet = System.Collections.Generic.HashSet<int> ()

    while (j < vertices.Length) do

        let start' = vertices.[i]
        let end' = vertices.[j]
        let dir' = end' - start' |> Vector2.Normalize

        let v1 = ray.Origin - start'
        let v2 = end' - start'
        let v3 = Vector2 (-ray.Direction.Y, ray.Direction.X)

        let t1 = perpDot v2 v1 / Vector2.Dot (v2, v3)
        let t2 = Vector2.Dot (v1, v3) / Vector2.Dot (v2, v3)

        if (t1 >= 0.f && t2 >= 0.f && t2 <= 1.f) then
            segmentsHit.Add(i, j, ray.GetPoint (t1))

        i <- j
        j <- j + 1

    if segmentsHit.Count = 0 then
        None
    else

        let yopac =
            segmentsHit
            |> Seq.sortBy (fun (i, j, p) ->
                isPointOnLeftSide vertices.[j] vertices.[i] ray.Origin
            )
            |> Seq.toArray


        yopac
        |> Seq.rev
        |> Seq.minBy (fun (i, j, p) ->
            (ray.Origin - p).Length()
        )
        |> Some

let inline pointInsideTriangle p v =
    Polygon.isPointInside p (Polygon.create v)


let computeVertices (vertices: Vector2 seq) f =

    let rec computeVertices (recursiveSteps: int) (vertices: Vector2 ResizeArray) currentIndex = 
        if recursiveSteps > vertices.Count then
            failwith "Unable to triangulate"
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

let decomposeTree (tree: PolygonTree) =

    let mutable vertices = tree.Polygon |> Polygon.vertices

    tree.Children
    |> List.sortByDescending (fun childTree -> 
        let yopac =
            childTree.Polygon 
            |> Polygon.vertices 
            |> Array.maxBy (fun x -> x.X)

        yopac.X
    )
    |> List.iteri (fun i childTree ->

        if childTree.Children.Length > 0 then
            failwith "butt"

        if true then

            let childVertices = childTree.Polygon |> Polygon.vertices

            let childMax = childVertices |> Array.maxBy (fun x -> x.X)

            let ray = { Origin = childMax; Direction = Vector2.UnitX }

            match rayIntersection ray vertices with
            | Some (edge1Index, edge2Index, pt) ->

                let mutable replaceIndex = None
                let childMaxIndex = childVertices |> Array.findIndex (fun x -> x = childMax)

                let v1 = pt
                let v2 = childVertices.[childMaxIndex]
                let v3 = vertices.[edge2Index]

                let reflexes = Array.zeroCreate vertices.Length
                for i = 0 to vertices.Length - 1 do
                    let prev =
                        if i = 0 then
                            vertices.Length - 1
                        else
                            i - 1

                    let next =
                        if i = vertices.Length - 1 then
                            0
                        else
                            i + 1

                    reflexes.[i] <- isReflexVertex vertices.[prev] vertices.[next] vertices.[i]
                         

                match
                    vertices |> Array.filter (fun x ->
                        x <> v1 && x <> v2 && x <> v3 &&
                        pointInsideTriangle x [|v1;v2;v3|]
                    ) with
                | [||] -> ()
                | points ->
                    //try
                    replaceIndex <-
                        let p =
                            points
                            |> Array.filter (fun x ->
                                let index = Array.findIndex (fun z -> z = x) vertices
                                reflexes.[index]
                            )
                            |> Array.maxBy (fun x -> Vector2.Dot (x - v2 |> Vector2.Normalize, v1 - v2 |> Vector2.Normalize))
                        Array.findIndex (fun x -> x = p) vertices
                        |> Some
                    //with | _ -> ()

                let linkedList = vertices |> System.Collections.Generic.List

                if not (Polygon.isArrangedClockwise (Polygon.create childVertices)) then
                    failwith "butt"

                let mutable ii = childMaxIndex
                let mutable count = 0
                let linkedList2 = System.Collections.Generic.List ()

               
                //linkedList2.Add(pt)

                match replaceIndex with
                | None ->
                    linkedList2.Add(vertices.[edge2Index])
                | Some index ->
                    linkedList2.Add(vertices.[index])

                while (count < childVertices.Length) do
                    linkedList2.Add(childVertices.[ii])
                    ii <-
                        if ii + 1 >= childVertices.Length then
                            0
                        else
                            ii + 1
                    count <- count + 1


                linkedList2.Add(childVertices.[childMaxIndex])
                //linkedList2.Add(pt)
                //linkedList.InsertRange(edge2Index, linkedList2)
                match replaceIndex with
                | None ->
                    linkedList.InsertRange(edge2Index, linkedList2)
                | Some index ->
                    linkedList.InsertRange(index, linkedList2)

                vertices <- (linkedList |> Seq.toArray)


            | _ -> ()
    )  

    Polygon.create vertices

let computeTree (tree: PolygonTree) =

    if tree.Children.IsEmpty then
        match compute tree.Polygon with
        | None -> [ ]
        | Some triangles ->
            [ triangles ]
    else


        let triangles = ResizeArray<Triangle2D> ()

        let vertices = 
            match compute (decomposeTree tree) with
            | None -> [||]
            | Some triangles ->
                triangles
                |> Array.map (fun x -> [|x.X;x.Y;x.Z|])
                |> Array.reduce Array.append

        let triangles = ResizeArray<Triangle2D> ()
        let mutable i = 0
        while (i < vertices.Length) do
            Triangle2D (
                vertices.[i],
                vertices.[i + 1],
                vertices.[i + 2]
            )
            |> triangles.Add
            i <- i + 3

        [
            triangles
            |> Seq.toArray
        ]
