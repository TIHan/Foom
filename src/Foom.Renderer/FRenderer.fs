namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

[<ReferenceEquality>]
type Mesh =
    {
        Position: Vector3ArrayBuffer
        Uv: Vector2ArrayBuffer
    }

[<ReferenceEquality>]
type Material =
    {
        VertexShaderFileName: string
        FragmentShaderFileName: string
        Texture: Texture2DBuffer option
        Color: Color
        mutable ShaderProgramId: int option
    }

[<ReferenceEquality>]
type FRendererBucket =
    {
        GetTransforms: (unit -> Matrix4x4) ResizeArray

        Meshes: Mesh ResizeArray
        Materials: Material ResizeArray
    }

    member this.Add (material: Material, mesh: Mesh, getTransform: unit -> Matrix4x4) =
        this.Materials.Add (material)
        this.Meshes.Add (mesh)
        this.GetTransforms.Add (getTransform)


type ShaderProgramId = int
type TextureId = int

type FRenderer =
    {
        TextureRenderLookup: Dictionary<ShaderProgramId, Dictionary<TextureId, FRendererBucket>> 
    }

    member this.TryAdd (material: Material, mesh: Mesh, getTransform: unit -> Matrix4x4) =

        let add shaderProgramId =
            match this.TextureRenderLookup.TryGetValue (shaderProgramId) with
            | true, bucketLookup when material.Texture.IsSome && material.Texture.Value.Id > 0 ->

                let textureId = material.Texture.Value.Id

                let bucket =
                    match bucketLookup.TryGetValue (textureId) with
                    | true, bucket -> bucket
                    | _ ->
                        let bucket =
                            {
                                GetTransforms = ResizeArray ()
                                Meshes = ResizeArray ()
                                Materials = ResizeArray ()
                            }
                        bucketLookup.Add (textureId, bucket)
                        bucket

                bucket.Add (material, mesh, getTransform)
            | _ -> ()

        material.ShaderProgramId
        |> Option.iter add
