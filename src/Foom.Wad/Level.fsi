namespace Foom.Wad.Level

open Foom.Wad.Geometry
open Foom.Wad.Level.Structures

type Level = 
    {
        Sectors: Sector [] 
    }

    member CalculatePolygonTrees : unit -> PolygonTree list
