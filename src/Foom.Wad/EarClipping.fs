[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Wad.Geometry

type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new (x, y, z) = { X = x; Y = y; Z = z }

let inline isReflexVertex (prev: Vector2) (next: Vector2) (vertex: Vector2) =
    let p1 = prev - vertex
    let p2 = next - vertex
    Vector3.Cross(Vector3 (p1.X, p1.Y, 0.f), Vector3 (p2.X, p2.Y, 0.f)).Z < 0.f

//create a list of the vertices (perferably in CCW order, starting anywhere)
//while true
//  for every vertex
//    let pPrev = the previous vertex in the list
//    let pCur = the current vertex;
//    let pNext = the next vertex in the list
//    if the vertex is not an interior vertex (the wedge product of (pPrev - pCur) and (pNext - pCur) <= 0, for CCW winding);
//      continue;
//    if there are any vertices in the polygon inside the triangle made by the current vertex and the two adjacent ones
//      continue;
//    create the triangle with the points pPrev, pCur, pNext, for a CCW triangle;
//    remove pCur from the list;
//  if no triangles were made in the above for loop
//    break;
let compute polygon =

    let triangles = ResizeArray<Triangle2D> ()

    let rec compute (recursiveSteps: int) (vertices: Vector2 ResizeArray) currentIndex =

        if recursiveSteps > vertices.Count then
            failwith "Unable to triangulate"

        if vertices.Count < 3 then
            triangles
        elif vertices.Count = 3 then
            triangles.Add (Triangle2D (vertices.[2], vertices.[1], vertices.[0]))

            triangles
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

        let triangle = Triangle2D (pNext, pCur, pPrev)

        let anyPointsInsideTriangle =
            vertices
            |> Seq.exists (fun x ->
                (x <> pPrev) && (x <> pCur) && (x <> pNext) && (Polygon.isPointInside x (Polygon.create [|triangle.X;triangle.Y;triangle.Z|]))
            )
  
        if isReflexVertex pPrev pNext pCur || anyPointsInsideTriangle then
            let nextIndex =
                if currentIndex >= (vertices.Count - 1) then
                    0
                else
                    currentIndex + 1
            compute (recursiveSteps + 1) (vertices) nextIndex
        else
            vertices.RemoveAt(currentIndex)

            let nextIndex =
                if currentIndex >= (vertices.Count - 1) then
                    0
                else
                    currentIndex + 1

            triangles.Add (triangle)
            compute 0 vertices nextIndex

    let triangles = compute 0 (ResizeArray (Polygon.vertices polygon)) 0

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

    //    vertices <- (linkedList |> Seq.toArray)
    //)        

    //[ Polygon.create vertices ]
