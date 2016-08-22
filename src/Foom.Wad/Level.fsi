namespace Foom.Wad.Level

open System.Numerics

open Foom.Wad.Geometry
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
        TextureName: string option
        TextureOffsetX: int
        TextureOffsetY: int
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
        Special: WallSpecial
    }

type Ceiling =
    {
        Vertices: Vector3 []
        Height: int
        TextureName: string option
    }

type Floor =
    {
        Vertices: Vector3 []
        Height: int
        TextureName: string option
    }

type Flat =
    {
        SectorId: int
        Ceiling: Ceiling
        Floor: Floor
    }

[<NoComparison; ReferenceEquality>]
type Level =
    internal {
        sectors: Sector []
    }

    member Sectors : Sector seq

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wall =

    val createUV : width: int -> height: int -> Wall -> Vector2 []

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Flat =

    val createFloorUV : width: int -> height: int -> Flat -> Vector2 []

    val createCeilingUV : width: int -> height: int -> Flat -> Vector2 []

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    val lightLevelBySectorId : sectorId: int -> Level -> byte

    val createFlats : sectorId: int -> Level -> Flat seq

    val createWalls : sectorId: int -> Level -> Wall seq