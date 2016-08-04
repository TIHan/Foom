namespace Foom.Wad.Level

open System.Numerics
open System.Collections.Immutable

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type LinedefTracer = { 
    endVertex: Vector2
    currentVertex: Vector2
    currentLinedef: Linedef
    linedefs: Linedef list
    visitedLinedefs: ImmutableHashSet<Linedef>
    path: Linedef list }

type LinedefPolygon = 
    {
        Linedefs: Linedef list
        Inner: LinedefPolygon list
    }

    // http://alienryderflex.com/polygon/
    member this.IsPointInside (point: Vector2) =
        let linedefs = this.Linedefs
        let mutable j = linedefs.Length - 1
        let mutable c = false

        for i = 0 to linedefs.Length - 1 do
            let linedef = linedefs.[i]

            let xp1 = linedef.Start.X
            let xp2 = linedef.End.X
            let yp1 = linedef.Start.Y
            let yp2 = linedef.End.Y

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

    let isPointOnFrontSide (p: Vector2) (linedef: Linedef) =
        match linedef.FrontSidedef.IsSome, linedef.BackSidedef.IsSome with
        | false, false -> failwith "Both, FrontSidef and BackSidedef, can't have None."
        | true, true -> failwith "Both, FrontSidef and BackSidedef, can't have Some."
        | isFront, _ ->
            if isFront
            then isPointOnLeftSide linedef.End linedef.Start p
            else isPointOnLeftSide linedef.Start linedef.End p

    let findClosestLinedef (linedefs: Linedef list) =
        let s = linedefs |> List.minBy (fun x -> x.Start.X)
        let e = linedefs |> List.minBy (fun x -> x.End.X)

        let v =
            if s.Start.X <= e.End.X 
            then s.Start
            else e.End

        match linedefs |> List.tryFind (fun x -> x.Start.Equals v) with
        | None -> linedefs |> List.find (fun x -> x.End.Equals v)
        | Some linedef -> linedef

    let inline nonVisitedLinedefs tracer = 
        tracer.linedefs |> List.filter (not << tracer.visitedLinedefs.Contains)

    let visit (linedef: Linedef) (tracer: LinedefTracer) =
        { tracer with
            currentVertex = if linedef.FrontSidedef.IsSome then linedef.End else linedef.Start
            currentLinedef = linedef
            visitedLinedefs = tracer.visitedLinedefs.Add linedef
            path = linedef :: tracer.path }  

    let create linedefs =
        let linedef = findClosestLinedef linedefs

        { endVertex = if linedef.FrontSidedef.IsSome then linedef.Start else linedef.End
          currentVertex = if linedef.FrontSidedef.IsSome then linedef.End else linedef.Start
          currentLinedef = linedef
          linedefs = linedefs
          visitedLinedefs = ImmutableHashSet<Linedef>.Empty.Add linedef
          path = [linedef] }

    let inline isFinished tracer = tracer.currentVertex.Equals tracer.endVertex 

    let inline currentDirection tracer =
        let v =
            if tracer.currentLinedef.FrontSidedef.IsSome
            then tracer.currentLinedef.Start
            else tracer.currentLinedef.End

        Vector2.Normalize (v - tracer.currentVertex)

    let tryVisitNextLinedef tracer =
        if isFinished tracer
        then tracer, false
        else
            match
                tracer.linedefs
                |> List.filter (fun l -> 
                    (tracer.currentVertex.Equals (if l.FrontSidedef.IsSome then l.Start else l.End)) && 
                    not (tracer.visitedLinedefs.Contains l)) with
            | [] -> tracer, false
            | [linedef] -> visit linedef tracer, true
            | linedefs ->
                let dir = currentDirection tracer

                let linedef =
                    linedefs
                    |> List.minBy (fun l ->
                        let v = if l.FrontSidedef.IsSome then l.End else l.Start                          
                        let result = Vector2.Dot (dir, Vector2.Normalize (tracer.currentVertex - v))

                        if isPointOnFrontSide v tracer.currentLinedef
                        then result
                        else 2.f + (result * -1.f))

                visit linedef tracer, true

    let run linedefs =
        let rec f (paths: Linedef list list) (originalTracer: LinedefTracer) (tracer: LinedefTracer) =
            match tryVisitNextLinedef tracer with
            | tracer, true -> f paths originalTracer tracer
            | tracer, _ -> 
                let isFinished = isFinished tracer
                let tracer = if isFinished then tracer else originalTracer
                let paths = if isFinished then (tracer.path :: paths) else paths

                match nonVisitedLinedefs tracer with
                | [] -> paths
                | linedefs ->
                    let tracer = create linedefs
                    f paths tracer tracer

        let tracer =
            linedefs
            |> Seq.filter (fun x -> 
                not (x.FrontSidedef.IsSome && x.BackSidedef.IsSome) &&
                not (x.FrontSidedef.IsNone && x.BackSidedef.IsNone)) 
            |> Seq.distinctBy (fun x -> x.Start, x.End)
            |> List.ofSeq
            |> create
                        
        f [] tracer tracer   

    let run2 linedefs =
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
                                polygon.IsPointInside linedef.Start &&
                                polygon.IsPointInside linedef.End
                            )

                          

                        let polygon =
                            if innerLinedefs.Length > 0 then
                                let tracer = create innerLinedefs
                                { polygon with Inner = f [] tracer tracer }
                            else
                                polygon


                        match linedefs with
                        | [] -> polygon :: polygons
                        | linedefs ->
                            let tracer = create linedefs
                            f (polygon :: polygons) tracer tracer


                else
                    match nonVisitedLinedefs tracer with
                    | [] -> polygons
                    | linedefs ->
                        let tracer = create linedefs
                        f polygons originalTracer originalTracer

        let tracer =
            linedefs
            |> Seq.filter (fun x -> 
                not (x.FrontSidedef.IsSome && x.BackSidedef.IsSome) &&
                not (x.FrontSidedef.IsNone && x.BackSidedef.IsNone)) 
            |> Seq.distinctBy (fun x -> x.Start, x.End)
            |> List.ofSeq
            |> create
                        
        f [] tracer tracer  

module Polygon =

    let ofLinedefs (linedefs: Linedef list) =
        let vertices =
            linedefs
            |> List.map (fun x -> 
                if x.FrontSidedef.IsSome then x.Start else x.End) 
            |> Array.ofList
        Polygon.create vertices.[..vertices.Length - 1]