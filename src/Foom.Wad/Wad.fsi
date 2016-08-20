namespace Foom.Wad

open System
open System.IO

open Foom.Wad.Pickler
open Foom.Wad.Level

type Texture =
    {
        Data: Pixel [,]
        Name: string
    }

[<Sealed>]
type Wad

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wad =

    val create : Stream -> Wad

    val findLevel : levelName: string -> wad: Wad -> Level

    val tryFindTexture : textureName: string -> wad: Wad -> Texture option

    val tryFindFlatTexture : textureName: string -> wad: Wad -> Texture option

    val iterFlatTextureName : (string -> unit) -> wad: Wad -> unit

    val iterTextureName : (string -> unit) -> wad: Wad -> unit