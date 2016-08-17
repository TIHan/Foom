namespace Foom.Wad.Geometry

open System
open System.Numerics

[<Struct>]
type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new (x, y, z) = { X = x; Y = y; Z = z }

type Polygon2D =
    {
        Vertices: Vector2 []
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon2D =

    let create vertices = { Vertices = vertices }

    let sign = function
        | x when x <= 0.f -> false
        | _ -> true

    let inline cross v1 v2 = (Vector3.Cross (Vector3(v1, 0.f), Vector3(v2, 0.f))).Z
        
    let isArrangedClockwise poly =
        let vertices = poly.Vertices
        let length = vertices.Length

        vertices
        |> Array.mapi (fun i y ->
            let x =
                match i with
                | 0 -> vertices.[length - 1]
                | _ -> vertices.[i - 1]
            cross x y)                
        |> Array.reduce ((+))
        |> sign

    // http://alienryderflex.com/polygon/
    let isPointInside (point: Vector2) poly =
        let vertices = poly.Vertices
        let mutable j = vertices.Length - 1
        let mutable c = false

        for i = 0 to vertices.Length - 1 do
            let xp1 = vertices.[i].X
            let xp2 = vertices.[j].X
            let yp1 = vertices.[i].Y
            let yp2 = vertices.[j].Y

            if
                ((yp1 > point.Y) <> (yp2 > point.Y)) &&
                (point.X < (xp2 - xp1) * (point.Y - yp1) / (yp2 - yp1) + xp1) then
                c <- not c
            else ()

            j <- i
        c

type Polygon2DTree = 
    {
        Polygon: Polygon2D
        Children: Polygon2DTree list
    }

type AAB2D =
    {
        Min: Vector2
        Max: Vector2
    }

type AABB2D =
    {
        Center: Vector2
        HalfSize: Vector2
    }

    member this.Min = this.Center + this.HalfSize

    member this.Max = this.Center - this.HalfSize

    // TODO: Yea, this is commented out. Let's fix this later.
    member this.Contains (v: Vector2) =
        v.X > this.Min.X && v.X < this.Max.X &&
        v.Y > this.Min.Y && v.Y < this.Max.Y
    //if(vecPoint.x > tBox.m_vecMin.x && vecPoint.x < tBox.m_vecMax.x &&
    //vecPoint.y > tBox.m_vecMin.y && vecPoint.y < tBox.m_vecMax.y &&
    //vecPoint.z > tBox.m_vecMin.z && vecPoint.z < tBox.m_vecMax.z)
    //{
    //    return true;
    //}
        //let distance = this.Center - v

        //(this.Center + this.HalfSize).Length () > distance.Length ()
        ////let x = abs distance.X
        ////let y = abs distance.Y
        ////abs distance.X <= this.HalfSize.X &&
        ////abs distance.Y <= this.HalfSize.Y

    member this.Contains (aabb: AABB2D) =
        let p1 = this.Contains (aabb.Max)
        let p2 = this.Contains (aabb.Min)
        let p3 = this.Contains (Vector2 (aabb.Max.X, aabb.Min.Y))
        let p4 = this.Contains (Vector2 (aabb.Min.X, aabb.Max.Y))
        p1 || p2 || p3 || p4

    static member FromAAB2D (aab: AAB2D) =
        {
            Center = (aab.Min + aab.Max) * 0.5f
            HalfSize = (aab.Min - aab.Max) * 0.5f
        }

// Note: I probably built this wrong.  
type QuadTree<'T> =
    {
        Capacity: int
        Bounds: AABB2D

        mutable NorthWest: QuadTree<'T> option
        mutable NorthEast: QuadTree<'T> option
        mutable SouthWest: QuadTree<'T> option
        mutable SouthEast: QuadTree<'T> option

        Items: ResizeArray<'T>
        ItemsBounds: ResizeArray<AABB2D>
    }
    
    static member Create (bounds, capacity) : QuadTree<'T> =
        {
            Capacity = capacity
            Bounds = bounds
            NorthWest = None
            NorthEast = None
            SouthWest = None
            SouthEast = None

            Items = ResizeArray ()
            ItemsBounds = ResizeArray<AABB2D> ()
        }

    member this.Subdivide () =
        let half = this.Bounds.HalfSize / 2.f

        let northWest =
            {
                Center = 
                    Vector2 (
                        this.Bounds.Center.X - half.X,
                        this.Bounds.Center.Y + half.Y
                    )
                HalfSize = half
            }

        let northEast =
            {
                Center = 
                    Vector2 (
                        this.Bounds.Center.X + half.X,
                        this.Bounds.Center.Y + half.Y
                    )
                HalfSize = half
            }

        let southWest =
            {
                Center = 
                    Vector2 (
                        this.Bounds.Center.X - half.X,
                        this.Bounds.Center.Y - half.Y
                    )
                HalfSize = half
            }

        let southEast =
            {
                Center = 
                    Vector2 (
                        this.Bounds.Center.X + half.X,
                        this.Bounds.Center.Y - half.Y
                    )
                HalfSize = half
            }
        
        this.NorthWest <- QuadTree<'T>.Create (northWest, this.Capacity) |> Some
        this.NorthEast <- QuadTree<'T>.Create (northEast, this.Capacity) |> Some
        this.SouthWest <- QuadTree<'T>.Create (southWest, this.Capacity) |> Some
        this.SouthEast <- QuadTree<'T>.Create (southEast, this.Capacity) |> Some


    member this.Insert (item: 'T, aabb: AABB2D) =
        if (this.Bounds.Contains (aabb) |> not) then
            false
        else

            if (this.Items.Count < this.Capacity) then
                this.Items.Add (item)
                this.ItemsBounds.Add (aabb)
                true
            else

                if this.NorthWest.IsNone then
                    this.Subdivide ()

                this.NorthWest.Value.Insert (item, aabb) |> ignore
                this.NorthEast.Value.Insert (item, aabb) |> ignore
                this.SouthWest.Value.Insert (item, aabb) |> ignore
                this.SouthEast.Value.Insert (item, aabb) |> ignore
                true

    member this.Query (range: AABB2D) =

        if (this.Bounds.Contains (range) |> not) then
            ResizeArray<'T> ()
        else
            let items = ResizeArray<'T> ()

            for i = 0 to this.Items.Count - 1 do
                let item = this.ItemsBounds.[i]
                if (range.Contains (item)) then
                    items.Add (this.Items.[i]) |> ignore

            if this.NorthWest.IsSome then
                items.AddRange (this.NorthWest.Value.Query (range))
                items.AddRange (this.NorthEast.Value.Query (range))
                items.AddRange (this.SouthWest.Value.Query (range))
                items.AddRange (this.SouthEast.Value.Query (range))

            items

    member this.ForEachBounds f =
        f this.Bounds

        if this.NorthWest.IsSome then
            this.NorthWest.Value.ForEachBounds f
            this.NorthEast.Value.ForEachBounds f
            this.SouthWest.Value.ForEachBounds f
            this.SouthEast.Value.ForEachBounds f
