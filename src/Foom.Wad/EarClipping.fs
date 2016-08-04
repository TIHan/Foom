[<RequireQualifiedAccess>]
module Foom.Wad.Geometry.Triangulation.EarClipping

open System.Numerics

open Foom.Wad.Geometry


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
let compute ((Polygon vertices) as polygon: Polygon) =
    let triangles = ResizeArray<Polygon> ()

    let rec compute (vertices: Vector2 ResizeArray) currentIndex =
        if vertices.Count < 3 then
            triangles
        elif vertices.Count = 3 then
            Polygon (vertices |> Seq.toArray)
            |> triangles.Add

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

        let p1 = pPrev - pCur
        let p2 = pNext - pCur
        let wedgeProduct = Vector3.Cross (Vector3 (p1.X, p1.Y, 0.f), Vector3 (p2.X, p2.Y, 0.f))

        let triangle = Polygon [|pPrev;pCur;pNext|]

        let anyPointsInsideTriangle =
            vertices
            |> Seq.exists (fun x ->
                (x <> pPrev) && (x <> pCur) && (x <> pNext) && (Polygon.isPointInside x triangle)
            )
  
        if (wedgeProduct.Z < 0.f) || anyPointsInsideTriangle then
            let nextIndex =
                if currentIndex >= (vertices.Count - 1) then
                    0
                else
                    currentIndex + 1
            compute (vertices) nextIndex
        else
            vertices.RemoveAt(currentIndex)

            let nextIndex =
                if currentIndex >= (vertices.Count - 1) then
                    0
                else
                    currentIndex + 1

            triangles.Add(Polygon [|pPrev;pCur;pNext|])
            compute vertices nextIndex

    let vertices =
        if Polygon.isArrangedClockwise polygon then
            vertices |> Array.rev
        else
            vertices

    compute (ResizeArray(vertices)) 0
    |> List.ofSeq