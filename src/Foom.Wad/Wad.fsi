namespace Foom.Wad

open System
open System.IO

open Foom.Wad.Level

[<Sealed>]
type Wad

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wad =

    val create : Stream -> Async<Wad>

    val findLevel : levelName: string -> wad: Wad -> Async<Level>
