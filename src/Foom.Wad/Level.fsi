namespace Foom.Wad.Level

open System.Numerics

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type Wall =
    {
        TextureName: string
        TextureOffsetX: int
        TextureOffsetY: int
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
    }

type Level = 
    {
        Sectors: Sector [] 
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wall =

    val createUV : width: int -> height: int -> Wall -> Vector2 []

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    val createWalls : Sector -> Level -> Wall seq
