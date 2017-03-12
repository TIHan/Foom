namespace Foom.Game.Assets

open Foom.Renderer

[<Sealed>]
type Texture (assetPath: string) = 

    member val AssetPath = assetPath

    member val Buffer = Texture2DBuffer ()
