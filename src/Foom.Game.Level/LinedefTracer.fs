namespace Foom.Game.Level

open System.Numerics
open System.Collections.Immutable

open Foom.Geometry

type LinedefTracer = 
    { 
        endVertex: Vector2
        currentVertex: Vector2
        currentLinedef: Wall
        linedefs: Wall list
        visitedLinedefs: ImmutableHashSet<Wall>
        path: Wall list 
        sectorId: int
    }

type LinedefPolygon = 
    {
        Linedefs: Wall list
        Inner: LinedefPolygon list
    }

    // http://alienryderflex.com/polygon/
    member this.IsPointInside (point: Vector2) =
        let linedefs = this.Linedefs
        let mutable j = linedefs.Length - 1
        let mutable c = false

        for i = 0 to linedefs.Length - 1 do
            let seg = linedefs.[i].Segment
            let a = seg.A
            let b = seg.B

            let xp1 = a.X
            let xp2 = b.X
            let yp1 = a.Y
            let yp2 = b.Y

            if
                ((yp1 > point.Y) <> (yp2 > point.Y)) &&
                (point.X < (xp2 - xp1) * (point.Y - yp1) / (yp2 - yp1) + xp1) then
                c <- not c
            else ()

            j <- i
        c

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module LinedefTracer =

    // http://stackoverflow.com/questions/1560492/how-to-tell-whether-a-point-is-to-the-right-or-left-side-of-a-line
    let inline isPointOnLeftSide (v1: Vector2) (v2: Vector2) (p: Vector2) =
        (v2.X - v1.X) * (p.Y - v1.Y) - (v2.Y - v1.Y) * (p.X - v1.X) > 0.f

    let isPointOnFrontSide (p: Vector2) (linedef: Wall) (tracer: LinedefTracer) =
        let seg = linedef.Segment
        let a = seg.A
        let b = seg.B

        if linedef.FrontSide.IsSome && linedef.FrontSide.Value.SectorId = tracer.sectorId
        then isPointOnLeftSide b a p
        else isPointOnLeftSide a b p

    let findClosestLinedef (linedefs: Wall list) =
        let s = linedefs |> List.minBy (fun x -> (LineSegment2D.startPoint x.Segment).X)
        let e = linedefs |> List.minBy (fun x -> (LineSegment2D.endPoint x.Segment).X)

        let v =
            if (LineSegment2D.startPoint s.Segment).X <= (LineSegment2D.endPoint e.Segment).X 
            then LineSegment2D.startPoint s.Segment
            else LineSegment2D.endPoint e.Segment

        match linedefs |> List.tryFind (fun x -> (LineSegment2D.startPoint x.Segment).Equals v) with
        | None -> linedefs |> List.find (fun x -> (LineSegment2D.endPoint x.Segment).Equals v)
        | Some linedef -> linedef

    let inline nonVisitedLinedefs tracer = 
        tracer.linedefs |> List.filter (not << tracer.visitedLinedefs.Contains)

    let visit (linedef: Wall) (tracer: LinedefTracer) =
        { tracer with
            currentVertex = if linedef.FrontSide.IsSome && linedef.FrontSide.Value.SectorId = tracer.sectorId then linedef.Segment.B else linedef.Segment.A
            currentLinedef = linedef
            visitedLinedefs = tracer.visitedLinedefs.Add linedef
            path = tracer.path @ [linedef] }  

    let create sectorId linedefs =
        let linedef = findClosestLinedef linedefs

        { 
            endVertex = if linedef.FrontSide.IsSome && linedef.FrontSide.Value.SectorId = sectorId then linedef.Segment.A else linedef.Segment.B
            currentVertex = if linedef.FrontSide.IsSome && linedef.FrontSide.Value.SectorId = sectorId then linedef.Segment.B else linedef.Segment.A
            currentLinedef = linedef
            linedefs = linedefs
            visitedLinedefs = ImmutableHashSet<Wall>.Empty.Add linedef
            path = [linedef]
            sectorId = sectorId
        }

    let inline isFinished tracer = tracer.currentVertex.Equals tracer.endVertex && tracer.path.Length >= 3

    let inline currentDirection tracer =
        let v =
            if tracer.currentLinedef.FrontSide.IsSome && tracer.currentLinedef.FrontSide.Value.SectorId = tracer.sectorId
            then tracer.currentLinedef.Segment.A
            else tracer.currentLinedef.Segment.B

        Vector2.Normalize (v - tracer.currentVertex)

    let tryVisitNextLinedef tracer =
        if isFinished tracer
        then tracer, false
        else
            match
                tracer.linedefs
                |> List.filter (fun l -> 
                    (tracer.currentVertex.Equals (if l.FrontSide.IsSome && l.FrontSide.Value.SectorId = tracer.sectorId then l.Segment.A else l.Segment.B)) && 
                    not (tracer.visitedLinedefs.Contains l)) with
            | [] -> tracer, false
            | [linedef] -> visit linedef tracer, true
            | linedefs ->
                let dir = currentDirection tracer

                let linedef =
                    linedefs
                    |> List.minBy (fun l ->
                        let v = if l.FrontSide.IsSome && l.FrontSide.Value.SectorId = tracer.sectorId then l.Segment.B else l.Segment.A                       
                        let result = Vector2.Dot (dir, Vector2.Normalize (tracer.currentVertex - v))

                        if isPointOnFrontSide v tracer.currentLinedef tracer
                        then result
                        else 2.f + (result * -1.f))

                visit linedef tracer, true

    let run sectorId level = 
        let rec f  (polygons: LinedefPolygon list) (originalTracer: LinedefTracer) (tracer: LinedefTracer) =
            match tryVisitNextLinedef tracer with
            | tracer, true -> f polygons originalTracer tracer
            | tracer, _ ->
                if isFinished tracer then

                    let polygon = 
                        {
                            Linedefs = tracer.path
                            Inner = [ ]
                        }

                    match nonVisitedLinedefs tracer with
                    | [] -> polygon :: polygons
                    | linedefs ->

                        let innerLinedefs, linedefs =
                            linedefs
                            |> List.partition (fun linedef ->
                                polygon.IsPointInside linedef.Segment.A &&
                                polygon.IsPointInside linedef.Segment.B
                            )

                          

                        let polygon =
                            if innerLinedefs.Length > 0 then
                                let tracer = create tracer.sectorId innerLinedefs
                                { polygon with Inner = f [] tracer tracer }
                            else
                                polygon


                        match linedefs with
                        | [] -> polygon :: polygons
                        | linedefs ->
                            let tracer = create tracer.sectorId linedefs
                            f (polygon :: polygons) tracer tracer


                else
                    match nonVisitedLinedefs originalTracer with
                    | [] -> polygons // Used to return "polygons". Return nothing because something broke.
                               // We might need to handle it a better way.
                    | linedefs ->
                        let tracer = create originalTracer.sectorId linedefs
                        f polygons tracer tracer

        let linedefs = ResizeArray ()
        (sectorId, level)
        ||> Level.iterWallBySectorId (fun x ->
            let canUseLinedef =
                if (x.FrontSide.IsSome && x.BackSide.IsSome && x.FrontSide.Value.SectorId = sectorId && x.BackSide.Value.SectorId = sectorId) then
                    false
                else
                    not (x.FrontSide.IsNone && x.BackSide.IsNone) 

            if canUseLinedef then
                linedefs.Add x
        )

        let tracer =
            linedefs
            |> List.ofSeq
            |> create sectorId
                        
        f [] tracer tracer  

module Polygon =

    let ofLinedefs (linedefs: Wall list) sectorId =
        let vertices =
            linedefs
            |> List.map (fun x -> 
                if x.FrontSide.IsSome && x.FrontSide.Value.SectorId = sectorId then x.Segment.A else x.Segment.B) 
            |> Array.ofList
        Polygon2D.create vertices