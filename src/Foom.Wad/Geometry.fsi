namespace Foom.Wad.Geometry

open System.Numerics

[<Struct>]
type Triangle2D =

    val X : Vector2

    val Y : Vector2

    val Z : Vector2

    new : Vector2 * Vector2 * Vector2 -> Triangle2D

type Polygon2D =
    {
        Vertices: Vector2 []
    } 

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Polygon2D =

    val create : vertices: Vector2 [] -> Polygon2D

    val isArrangedClockwise : poly: Polygon2D -> bool

    val isPointInside : point: Vector2 -> poly: Polygon2D -> bool

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

    member Min : Vector2

    member Max : Vector2

    member Contains : Vector2 -> bool

    member Intersects : AABB2D -> bool

    static member FromAAB2D : AAB2D -> AABB2D 

[<Sealed>]
type QuadTree<'T> =

    static member Create : AABB2D * int -> QuadTree<'T>

    member Subdivide : unit -> unit

    member Insert : 'T * AABB2D -> bool

    member Query : AABB2D -> ResizeArray<'T>