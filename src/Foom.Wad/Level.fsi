namespace Foom.Wad

open System.Numerics
open System.Collections.Generic

open Foom.Geometry

[<Sealed>]
type Level =

    static member Create : Sector seq * Thing seq * Linedef ResizeArray * Dictionary<int, Linedef ResizeArray> -> Level

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Level =
        
    val iterLinedef : (Linedef -> unit) -> Level -> unit

    val iterLinedefBySectorId : (Linedef -> unit) -> sectorId: int -> Level -> unit

    val getSector : index: int -> Level -> Sector

    val tryGetSector : index: int -> Level -> Sector option

    val iteriSector : (int -> Sector -> unit) -> Level -> unit

    val tryFindPlayer1Start : Level -> Thing option

    val lightLevelBySectorId : sectorId: int -> Level -> byte
