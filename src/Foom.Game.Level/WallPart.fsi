namespace Foom.Game.Level

open System.Numerics

open Foom.Geometry

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type WallPart =
    {
        TextureOffsetX: int
        TextureOffsetY: int
        TextureName: string option
        TextureAlignment: TextureAlignment
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module WallPart =

    val updateUV : vertices: Vector3 [] -> uv: Vector2 [] -> width: int -> height: int -> WallPart -> unit

    val createUV : vertices: Vector3 [] -> width: int -> height: int -> WallPart -> Vector2 []
