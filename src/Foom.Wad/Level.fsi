namespace Foom.Wad

open System.Numerics

open Foom.Geometry

[<Sealed>]
type Level =

    static member Create : Sector seq * Thing seq -> Level

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =

    val getAABB : Level -> AABB2D
        
    val getSector : index: int -> Level -> Sector

    val tryGetSector : index: int -> Level -> Sector option

    val iteriSector : (int -> Sector -> unit) -> Level -> unit

    val sectorAt : Vector2 -> Level -> Sector option

    val getAdjacentSectors : sector: Sector -> Level -> Sector list

    val tryFindPlayer1Start : Level -> Thing option

    val lightLevelBySectorId : sectorId: int -> Level -> byte
