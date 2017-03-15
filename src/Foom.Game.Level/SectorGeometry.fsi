namespace Foom.Game.Level

open System.Numerics

[<Sealed>]
type SectorGeometry =

    member Vertices : Vector3 []

    member Height : float32

    member TextureName : string option

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module SectorGeometry =

    val create : vertices: Vector3 [] -> height: float32 -> textureName: string option -> SectorGeometry

    val changeHeight : height: float32 -> SectorGeometry -> unit

    val createUV : width: int -> height: int -> SectorGeometry -> Vector2 []
