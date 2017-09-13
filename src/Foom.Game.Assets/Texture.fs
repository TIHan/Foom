namespace Foom.Game.Assets

open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Renderer
open Foom.Collections

[<ReferenceEquality>]
type TextureKind =
    | Single of assetPath: string
    | Multi of assetPaths: string list

[<Sealed>]
type Texture () = 

    member val internal Id = CompactId.Zero with get, set

    member val Buffer = Texture2DBuffer ()

    member val Frames : Vector4 [] = [||] with get, set

type MeshInfo =
    {
        Position: Vector3 []
        Uv: Vector2 []
        Color: Vector4 []
    }

    member this.ToMesh () =
        let color =
            this.Color
            |> Array.map (fun c ->
                Vector4 (
                    single c.X / 255.f,
                    single c.Y / 255.f,
                    single c.Z / 255.f,
                    single c.W / 255.f)
            )
        Mesh (this.Position, this.Uv, color)

[<AbstractClass>]
type BaseMaterial (shader : BaseShader, texture : Texture) =

    member val Shader = shader

    member val Texture = texture

[<Sealed>]
type Material<'T when 'T :> MeshInput> (shader : Shader<'T>, texture) =
    inherit BaseMaterial (shader :> BaseShader, texture)

[<AbstractClass>]
type MaterialDescription () =

    abstract CreateMaterial : (TextureKind -> Texture) -> BaseMaterial

[<ReferenceEquality>]
type ShaderPassDescription<'T when 'T :> MeshInput> = ShaderPassDescription of createProperties : ('T -> ShaderPassProperty list) * shaderProgramName : string

[<ReferenceEquality>]
type ShaderDescription<'T when 'T :> MeshInput> = ShaderDescription of order : int * pass : ShaderPassDescription<'T> * createInput : (ShaderInput -> 'T)

[<Sealed>]
type MaterialDescription<'T when 'T :> MeshInput> (shaderDesc : ShaderDescription<'T>, textureKind : TextureKind) =
    inherit MaterialDescription ()

    member this.ShaderDescription = shaderDesc

    member this.TextureKind = textureKind

    override this.CreateMaterial loadTexture =
        match shaderDesc with
        | ShaderDescription (order, pass, createInput) ->
            match pass with
            | ShaderPassDescription (createProps, shaderProgramName) ->
                let shaderPass = ShaderPass (createProps, shaderProgramName)
                let shader = Shader (order, shaderPass, createInput)
                Material (shader, loadTexture textureKind) :> BaseMaterial

[<AutoOpen>]
module RendererHelpers =

    let CreateShaderPass createProps shaderProgramName =
        ShaderPassDescription (createProps, shaderProgramName)

    let CreateShader createInput order pass = 
        ShaderDescription (order, pass, createInput)

type IAssetLoader =

    abstract LoadTextureFile : assetPath: string -> TextureFile

[<Sealed>]
type AssetManager (assetLoader: IAssetLoader) =

    let textures = CompactManager<Texture> (0)
    let textureEnum = ResizeArray<Texture> ()

    let materialLookup = Dictionary<MaterialDescription, BaseMaterial> ()
    let textureKindCache = Dictionary<TextureKind, Texture> ()

    member this.AssetLoader = assetLoader

    member this.LoadTexture (textureKind: TextureKind) =
        let texture =
            match textureKindCache.TryGetValue textureKind with
            | true, texture -> texture
            | _ -> 
                let texture = Texture ()
                texture.Id <- textures.Add texture
                textureKindCache.Add (textureKind, texture)
                texture

        if not texture.Buffer.HasData then

            match textureKind with
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

                texture.Frames <- frames

                if xOffset > 1024 then
                    failwith "finish texture packing implementation"
                    
                texture.Buffer.Set (textureFiles, maxWidth, maxHeight)

        texture

    member this.GetMaterial materialDesc =
        match materialLookup.TryGetValue (materialDesc) with
        | true, material -> material
        | _ ->
            let material = materialDesc.CreateMaterial this.LoadTexture
            materialLookup.Add (materialDesc, material)
            material
