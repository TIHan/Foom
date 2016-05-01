namespace Foom.Wad.Level

open System.Numerics
open System.Collections.Immutable

open Foom.Wad.Level.Structures

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module LinedefTracer =
    val run : Linedef seq -> Linedef list list