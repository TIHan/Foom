namespace Foom.Wad.Level.Structures

open System.Numerics

[<NoComparison; ReferenceEquality>]
type Linedef = {
    Start: Vector2
    End: Vector2
    FrontSidedef: Sidedef option
    BackSidedef: Sidedef option }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Linedef =
    val angle : Linedef -> single
