namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

type Shader =
    {
        ProgramId: int
    }

type Texture =
    {
        Buffer: Texture2DBuffer
    }

type Mesh =
    {
        Position: Vector3ArrayBuffer
        Uv: Vector2ArrayBuffer
    }

type Material =
    {
        Shader: Shader
        Texture: Texture
        Color: Color
    }

[<ReferenceEquality>]
type FRendererBucket =
    {
        GetTransforms: (unit -> Matrix4x4) ResizeArray

        Meshes: Mesh ResizeArray
        Materials: Material ResizeArray
        IdRefs: int Ref ResizeArray
    }

    member this.Add (material: Material, mesh: Mesh, getTransform: unit -> Matrix4x4) =
        let idRef = ref this.IdRefs.Count

        this.Materials.Add (material)
        this.Meshes.Add (mesh)
        this.GetTransforms.Add (getTransform)
        this.IdRefs.Add (idRef)

        idRef

    member this.RemoveById id =
        let lastIndex = this.IdRefs.Count - 1

        this.Materials.[id] <- this.Materials.[lastIndex]
        this.Meshes.[id] <- this.Meshes.[lastIndex]
        this.GetTransforms.[id] <- this.GetTransforms.[lastIndex]
        this.IdRefs.[id] <- this.IdRefs.[lastIndex]
        this.IdRefs.[id] := id

        this.Materials.RemoveAt (lastIndex)
        this.Meshes.RemoveAt (lastIndex)
        this.GetTransforms.RemoveAt (lastIndex)
        this.IdRefs.RemoveAt (lastIndex)



type ShaderProgramId = int
type TextureId = int

type MeshRender =
    {
        ShaderProgramId: int
        TextureId: int
        IdRef: int ref
    }

type FRenderer =
    {
        Lookup: Dictionary<ShaderProgramId, Dictionary<TextureId, FRendererBucket>> 
    }

    static member Create () =
        {
            Lookup = Dictionary ()
        }

    member this.CreateShader (vertexShader, fragmentShader) =
        {
            ProgramId = Renderer.loadShaders vertexShader fragmentShader
        }

    member this.CreateTexture (bmp) =
        {
            Buffer = Texture2DBuffer (bmp)
        }

    member this.CreateMaterial (shader, texture, color) =
        {
            Shader = shader
            Texture = texture
            Color = color
        }

    member this.CreateMesh (position, uv) =
        {
            Position = Vector3ArrayBuffer (position)
            Uv = Vector2ArrayBuffer (uv)
        }

    member this.TryAdd (material: Material, mesh: Mesh, getTransform: unit -> Matrix4x4) =

        let addTexture (bucketLookup: Dictionary<TextureId, FRendererBucket>) textureId = 
            let bucket =
                match bucketLookup.TryGetValue (textureId) with
                | true, bucket -> bucket
                | _ ->
                    let bucket =
                        {
                            GetTransforms = ResizeArray ()
                            Meshes = ResizeArray ()
                            Materials = ResizeArray ()
                            IdRefs = ResizeArray ()
                        }
                    bucketLookup.Add (textureId, bucket)
                    bucket
            bucket

        let add shaderProgramId =
            let bucketLookup =
                match this.Lookup.TryGetValue (shaderProgramId) with
                | true, bucketLookup -> bucketLookup
                | _, _ ->
                    let bucketLookup = Dictionary ()
                    this.Lookup.Add (shaderProgramId, bucketLookup)
                    bucketLookup

            material.Texture.Buffer.TryBufferData () |> ignore

            let textureId = material.Texture.Buffer.Id

            let bucket = addTexture bucketLookup textureId

            let idRef = bucket.Add (material, mesh, getTransform)

            let render =
                {
                    ShaderProgramId = shaderProgramId
                    TextureId = textureId
                    IdRef = idRef
                }

            Some render

        add material.Shader.ProgramId

    member this.ProcessPrograms f =
        this.Lookup
        |> Seq.iter (fun pair ->
            let programId = pair.Key
            let textureLookup = pair.Value

            Renderer.useProgram programId

            textureLookup
            |> Seq.iter (fun pair ->
                let textureId = pair.Key
                let bucket = pair.Value

                f programId textureId bucket
            )
        )

    member this.Clear () =
        Renderer.clear ()

    member this.Draw (projection: Matrix4x4) (view: Matrix4x4) =

        Renderer.enableDepth ()

        let mvp = (projection * view) |> Matrix4x4.Transpose

        let mutable drawCalls = 0

        this.ProcessPrograms (fun programId textureId bucket ->

            let count = bucket.IdRefs.Count

            for i = 0 to count - 1 do

                let getTransform = bucket.GetTransforms.[i]
                let mesh = bucket.Meshes.[i]
                let material = bucket.Materials.[i]

                let position = mesh.Position
                let uv = mesh.Uv
                let color = material.Color
                let textureBuffer = material.Texture.Buffer

                let model = getTransform ()

                position.TryBufferData () |> ignore
                uv.TryBufferData () |> ignore

                let uniformColor = Renderer.getUniformLocation programId "uni_color"
                let uniformProjection = Renderer.getUniformLocation programId "uni_projection"

                Renderer.setUniformProjection uniformProjection mvp

                position.Bind ()
                Renderer.bindPosition programId

                uv.Bind ()
                Renderer.bindUv programId

                Renderer.setTexture programId textureBuffer.Id
                textureBuffer.Bind ()

                Renderer.setUniformColor uniformColor (Color.FromArgb (255, int color.R, int color.G, int color.B) |> RenderColor.OfColor)
                Renderer.drawTriangles 0 position.Length

                drawCalls <- drawCalls + 1
        )

        Renderer.disableDepth ()

        printfn "Draw Calls: %A" drawCalls
