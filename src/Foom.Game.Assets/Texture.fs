namespace Foom.Game.Assets

open Foom.Ecs
open Foom.Renderer

type TextureKind =
    | Single of assetPath: string
    | Multi of assetPaths: string list

[<Sealed>]
type Texture (kind: TextureKind) = 

    member val Kind = kind

    member val Buffer = Texture2DBuffer ()

    member val Frame = 0 with get, set

    member this.AssetPath =
        match kind with
        | Single x -> x
        | _ -> ""

type IAssetLoader =

    abstract LoadTextureFile : assetPath: string -> TextureFile

[<Sealed>]
type AssetManager (assetLoader: IAssetLoader) =

    member this.LoadTexture (texture: Texture) =
        match texture.Kind with
        | Single assetPath ->

            if not texture.Buffer.HasData then
                texture.Buffer.Set (assetLoader.LoadTextureFile assetPath)

        | _ -> ()