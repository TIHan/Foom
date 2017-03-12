namespace Foom.Game.Assets

open System.Numerics

open Foom.Ecs
open Foom.Renderer

type TextureKind =
    | Single of assetPath: string
    | Multi of assetPaths: string list

[<Sealed>]
type Texture (kind: TextureKind) = 

    let frameCount =
        match kind with
        | Single _ -> 1
        | Multi xs -> xs.Length

    member val Kind = kind

    member val Buffer = Texture2DBuffer ()

    member val FrameCount = frameCount

    member val Dimensions = ResizeArray<Vector2> ()

    member this.GetFrameDimension frame =
        if frame >= this.Dimensions.Count || frame < 0 then
            Vector2 (0.f, 0.f)
        else
            this.Dimensions.[frame]

    member this.AssetPath =
        match kind with
        | Single x -> x
        | _ -> ""

type IAssetLoader =

    abstract LoadTextureFile : assetPath: string -> TextureFile

[<Sealed>]
type AssetManager (assetLoader: IAssetLoader) =

    member this.LoadTexture (texture: Texture) =
        if not texture.Buffer.HasData then

            match texture.Kind with
            | Single assetPath ->

                let textureFile = assetLoader.LoadTextureFile assetPath
                texture.Buffer.Set (textureFile)
                texture.Dimensions.Add (Vector2 (single textureFile.Width, single textureFile.Height))

            | _ -> ()