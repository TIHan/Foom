[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Wad.Geometry

[<Struct>]
type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new (x, y, z) = { X = x; Y = y; Z = z }

let inline isReflexVertex (prev: Vector2) (next: Vector2) (vertex: Vector2) =
    let p1 = prev - vertex
    let p2 = next - vertex
    Vector3.Cross(Vector3 (p1.X, p1.Y, 0.f), Vector3 (p2.X, p2.Y, 0.f)).Z < 0.f

let computeVertices (vertices: Vector2 seq) f =

    let rec computeVertices (recursiveSteps: int) (vertices: Vector2 ResizeArray) currentIndex = 
        if recursiveSteps > vertices.Count then
            failwith "Unable to triangulate"

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
                (x <> pPrev) && (x <> pCur) && (x <> pNext) && (Polygon.isPointInside x (Polygon.create triangle))
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
        []
    else
        let result = 
            triangles
            |> Seq.toArray

        [ result ]

let computeTree (tree: PolygonTree) =

    compute tree.Polygon

    //if tree.Children.IsEmpty then
    //    compute tree.Polygon
    //else

    //let mutable vertices = tree.Polygon |> Polygon.vertices

    //tree.Children
    //|> List.iter (fun childTree ->
    //    let childVertices = childTree.Polygon |> Polygon.vertices

    //    let childMax = childVertices |> Array.maxBy (fun x -> x.X)
    //    let max = vertices |> Array.maxBy (fun x -> x.X)

    //    let maxIndex = vertices |> Array.findIndex (fun x -> x = max)
    //    let childMaxIndex = childVertices |> Array.findIndex (fun x -> x = childMax)

    //    let linkedList = vertices |> System.Collections.Generic.List


    //    let mutable i = childMaxIndex
    //    let mutable count = 0
    //    let linkedList2 = System.Collections.Generic.List ()

    //    while (count < childVertices.Length) do
    //        i <-
    //            if i + 1 >= childVertices.Length then
    //                0
    //            else
    //                i + 1
    //        linkedList2.Add(childVertices.[i])
    //        count <- count + 1

    //    let indy =
    //        if maxIndex + 1 >= linkedList.Count then
    //            0
    //        else
    //            maxIndex + 1

    //    linkedList2.Add(linkedList2.[0])
    //    linkedList2.Add(vertices.[maxIndex])



    //    linkedList.InsertRange(indy, linkedList2)

    //    vertices <- 
    //        compute (Polygon.create (linkedList |> Seq.toArray))
    //        |> Array.map (fun x -> [|x.X;x.Y;x.Z|])
    //        |> Array.reduce Array.append
    //)   

    //let triangles = ResizeArray<Triangle2D> ()
    //let mutable i = 0

    //while (i < vertices.Length) do
    //    Triangle2D (
    //        vertices.[i],
    //        vertices.[i + 1],
    //        vertices.[i + 2]
    //    )
    //    |> triangles.Add
    //    i <- i + 3
    //[
    //    triangles
    //    |> Seq.toArray
    //]
