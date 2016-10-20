namespace Foom.Level

open System.Numerics

[<Sealed>]
type FlatPart =

    member Vertices : Vector3 []

    member Height : float32

    member TextureName : string option

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module FlatPart =

    val create : vertices: Vector3 [] -> height: float32 -> textureName: string option -> FlatPart

    val changeHeight : height: float32 -> FlatPart -> unit

    val createUV : width: int -> height: int -> FlatPart -> Vector2 []
