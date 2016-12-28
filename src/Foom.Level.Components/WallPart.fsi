namespace Foom.Level

open System.Numerics

open Foom.Geometry

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type WallPartSide =
    {
        TextureOffsetX: int
        TextureOffsetY: int
        TextureName: string option
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
    }

type WallPart =
    {
        FrontSide: WallPartSide option
        BackSide: WallPartSide option
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module WallPart =

    val updateFrontUV : uv: Vector2 [] -> width: int -> height: int -> WallPart -> unit

    val createFrontUV : width: int -> height: int -> WallPart -> Vector2 []

    val updateBackUV : uv: Vector2 [] -> width: int -> height: int -> WallPart -> unit

    val createBackUV : width: int -> height: int -> WallPart -> Vector2 []
