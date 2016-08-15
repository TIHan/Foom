namespace Foom.Wad.Level

open System.Numerics

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type Level = 
    {
        Sectors: Sector [] 
    }

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

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    val createWalls : Sector -> Level -> Wall seq
