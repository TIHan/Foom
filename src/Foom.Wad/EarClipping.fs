[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Wad.Geometry

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

    let triangles = ResizeArray<Vector2 []> ()

    let rec compute (recursiveSteps: int) (vertices: Vector2 ResizeArray) currentIndex =

        if recursiveSteps > vertices.Count then
            failwith "Unable to triangulate"

        if vertices.Count < 3 then
            triangles
        elif vertices.Count = 3 then
            triangles.Add([|vertices.[2];vertices.[1];vertices.[0]|])

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

        let triangle = Polygon.create [|pNext;pCur;pPrev|]

        let anyPointsInsideTriangle =
            vertices
            |> Seq.exists (fun x ->
                (x <> pPrev) && (x <> pCur) && (x <> pNext) && (Polygon.isPointInside x triangle)
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

            triangles.Add([|pNext;pCur;pPrev|])
            compute 0 vertices nextIndex

    let triangles = compute 0 (ResizeArray (Polygon.vertices polygon)) 0

    if triangles.Count = 0 then
        []
    else
        let result = 
            triangles
            |> Seq.reduce Array.append

        [ Polygon.create result ]

let computeTree (tree: PolygonTree) =



    tree.Polygon
    |> compute
        
