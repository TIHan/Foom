namespace Foom.Wad

open System
open System.IO

open Foom.Wad.Pickler
open Foom.Wad.Level

type FlatTexture =
    {
        Pixels: Pixel []
        Name: string
    }

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

    val tryFindFlatTexture : textureName: string -> wad: Wad -> FlatTexture option

    val iterFlatTextureName : (string -> unit) -> wad: Wad -> unit

    val iterTextureName : (string -> unit) -> wad: Wad -> unit