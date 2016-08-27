namespace Foom.DataStructures

open System
open System.Numerics

open Foom.Math
open Foom.Geometry

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
            AABB2D.FromCenterAndHalfSize (
                Vector2 (
                    this.Bounds.Center.X - half.X,
                    this.Bounds.Center.Y + half.Y
                ),
                half
            )

        let northEast =
            AABB2D.FromCenterAndHalfSize (
                    Vector2 (
                        this.Bounds.Center.X + half.X,
                        this.Bounds.Center.Y + half.Y
                    ),
                half
            )

        let southWest =
            AABB2D.FromCenterAndHalfSize (
                Vector2 (
                    this.Bounds.Center.X - half.X,
                    this.Bounds.Center.Y - half.Y
                ),
                half
            )

        let southEast =
            AABB2D.FromCenterAndHalfSize (
                Vector2 (
                    this.Bounds.Center.X + half.X,
                    this.Bounds.Center.Y - half.Y
                ),
                half
            )
        
        this.NorthWest <- QuadTree<'T>.Create (northWest, this.Capacity) |> Some
        this.NorthEast <- QuadTree<'T>.Create (northEast, this.Capacity) |> Some
        this.SouthWest <- QuadTree<'T>.Create (southWest, this.Capacity) |> Some
        this.SouthEast <- QuadTree<'T>.Create (southEast, this.Capacity) |> Some


    member this.Insert (item: 'T, aabb: AABB2D) =
        let contains = this.Bounds.Contains (aabb)
        if (contains = ContainmentType.Intersects || contains = ContainmentType.Contains) then
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

    member this.Query (range: AABB2D, f) =
        let contains = this.Bounds.Contains (range)
        if (contains = ContainmentType.Intersects || contains = ContainmentType.Contains) then

            for i = 0 to this.Items.Count - 1 do
                let item = this.ItemsBounds.[i]
                let contains = range.Contains (item)
                if (contains = ContainmentType.Intersects || contains = ContainmentType.Contains) then
                    f this.Items.[i]

            if this.NorthWest.IsSome then
                this.NorthWest.Value.Query (range, f)
                this.NorthEast.Value.Query (range, f)
                this.SouthWest.Value.Query (range, f)
                this.SouthEast.Value.Query (range, f)

    member this.ForEachBounds f =
        f this.Bounds

        if this.NorthWest.IsSome then
            this.NorthWest.Value.ForEachBounds f
            this.NorthEast.Value.ForEachBounds f
            this.SouthWest.Value.ForEachBounds f
            this.SouthEast.Value.ForEachBounds f

