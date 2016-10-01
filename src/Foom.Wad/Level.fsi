namespace Foom.Wad.Level

open System.Numerics

open Foom.Geometry
open Foom.Wad.Level.Structures

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type WallSpecial =
    | Nothing
    | Door of ceilingSectorId: int

type Wall =
    {
        SectorId: int

        DefaultTextureName: string option

        TextureOffsetX: int
        TextureOffsetY: int
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
        Special: WallSpecial
    }

type Ceiling =
    {
        Vertices: Vector3 []

        DefaultHeight: int
        DefaultTextureName: string option
    }

type Floor =
    {
        Vertices: Vector3 []

        DefaultHeight: int
        DefaultTextureName: string option
    }

type Flat =
    {
        SectorId: int
        Ceiling: Ceiling
        Floor: Floor
    }

[<Sealed>]
type Level =

    static member Create : Sector seq * Thing seq -> Level

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wall =

    val updateUV : uv: Vector2 [] -> width: int -> height: int -> Wall -> unit

    val createUV : width: int -> height: int -> Wall -> Vector2 []

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Flat =

    val createFloorUV : width: int -> height: int -> Flat -> Vector2 []

    val createCeilingUV : width: int -> height: int -> Flat -> Vector2 []

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    val getAABB : Level -> AABB2D
        
    val getSector : index: int -> Level -> Sector

    val iteriSector : (int -> Sector -> unit) -> Level -> unit

    val sectorAt : Vector2 -> Level -> Sector option

    val getAdjacentSectors : sector: Sector -> Level -> Sector list

    val tryFindPlayer1Start : Level -> Thing option

    val lightLevelBySectorId : sectorId: int -> Level -> byte

    val createFlats : sectorId: int -> Level -> Flat seq

    val createWalls : sectorId: int -> Level -> Wall seq