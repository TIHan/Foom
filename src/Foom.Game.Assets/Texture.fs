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

    member val Frames : Vector4 [] = [||] with get, set

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
                texture.Frames <- [| Vector4 (0.f, 0.f, single textureFile.Width, single textureFile.Height) |]; 

            | Multi assetPaths ->

                let textureFiles =
                    assetPaths
                    |> List.map assetLoader.LoadTextureFile

                let maxWidth = 1024
                let maxHeight = 1024
               
                let mutable xOffset = 0

                let frames = Array.zeroCreate textureFiles.Length
                textureFiles
                |> List.iteri (fun i file ->
                    frames.[i] <- Vector4 (single xOffset, 0.f, single file.Width, single file.Height)

                    xOffset <- xOffset + file.Width
                )

                if xOffset > 1024 then
                    failwith "finish texture packing implementation"
                  
                texture.Buffer.Set (textureFiles, maxWidth, maxHeight)
