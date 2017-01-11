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
        Color: Vector4ArrayBuffer
        Center: Vector3ArrayBuffer
    }

type Material =
    {
        Shader: Shader
        Texture: Texture
    }

[<ReferenceEquality>]
type FRendererBucket =
    {
        Meshes: Mesh ResizeArray
        IdRefs: int Ref ResizeArray
    }

    member this.Add (mesh: Mesh) =
        let idRef = ref this.IdRefs.Count

        this.Meshes.Add (mesh)
        this.IdRefs.Add (idRef)

        idRef

    member this.RemoveById id =
        let lastIndex = this.IdRefs.Count - 1

        this.Meshes.[id] <- this.Meshes.[lastIndex]
        this.IdRefs.[id] <- this.IdRefs.[lastIndex]
        this.IdRefs.[id] := id

        this.Meshes.RemoveAt (lastIndex)
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
        Lookup: Dictionary<ShaderProgramId, Shader * Dictionary<TextureId, Texture * FRendererBucket>> 
        Transparents: ResizeArray<ShaderProgramId * Material * Mesh>
    }

    static member Create () =
        {
            Lookup = Dictionary ()
            Transparents = ResizeArray ()
        }

    member this.CreateShader (vertexShader, fragmentShader) =
        {
            ProgramId = Renderer.loadShaders vertexShader fragmentShader
        }

    member this.CreateTexture (bmp) =
        {
            Buffer = Texture2DBuffer (bmp)
        }

    member this.CreateMaterial (shader, texture) =
        {
            Shader = shader
            Texture = texture
        }

    member this.CreateMesh (position, uv, color: Color [], center) =
        {
            Position = Vector3ArrayBuffer (position)
            Uv = Vector2ArrayBuffer (uv)
            Color =
                color
                |> Array.map (fun c ->
                    Vector4 (
                        single c.R / 255.f,
                        single c.G / 255.f,
                        single c.B / 255.f,
                        single c.A / 255.f)
                )
                |> Vector4ArrayBuffer
            Center = Vector3ArrayBuffer (center)
        }

    member this.TryAdd (material: Material, mesh: Mesh) =

        material.Texture.Buffer.TryBufferData () |> ignore
        mesh.Position.TryBufferData () |> ignore
        mesh.Uv.TryBufferData () |> ignore

        // TODO: What shall we do with transparent textures? :D
//        if material.Texture.Buffer.IsTransparent then
//            None
//        else

        let addTexture (bucketLookup: Dictionary<TextureId, Texture * FRendererBucket>) texture = 
            let bucket =
                match bucketLookup.TryGetValue (texture.Buffer.Id) with
                | true, (_, bucket) -> bucket
                | _ ->
                    let bucket =
                        {
                            Meshes = ResizeArray ()
                            IdRefs = ResizeArray ()
                        }
                    bucketLookup.Add (texture.Buffer.Id, (texture, bucket))
                    bucket
            bucket

        let add shader =
            let bucketLookup =
                match this.Lookup.TryGetValue (shader.ProgramId) with
                | true, (_, bucketLookup) -> bucketLookup
                | _, _ ->
                    let bucketLookup = Dictionary ()
                    this.Lookup.Add (shader.ProgramId, (shader, bucketLookup))
                    bucketLookup

            material.Texture.Buffer.TryBufferData () |> ignore

            let bucket = addTexture bucketLookup material.Texture

            let idRef = bucket.Add (mesh)

            let render =
                {
                    ShaderProgramId = shader.ProgramId
                    TextureId = material.Texture.Buffer.Id
                    IdRef = idRef
                }

            Some render

        add material.Shader

    member this.ProcessPrograms f =
        this.Lookup
        |> Seq.iter (fun pair ->
            let programId = pair.Key
            let (shader, textureLookup) = pair.Value

            Renderer.useProgram programId

            textureLookup
            |> Seq.iter (fun pair ->
                let textureId = pair.Key
                let (texture, bucket) = pair.Value

                let count = bucket.Meshes.Count

                for i = 0 to count - 1 do

                    let mesh = bucket.Meshes.[i]

                    f shader texture mesh
            )
        )

    member this.Clear () =
        Renderer.clear ()

    member this.Draw (projection: Matrix4x4) (view: Matrix4x4) =

        Renderer.enableDepth ()

        let mutable drawCalls = 0

        this.ProcessPrograms (fun shader texture mesh ->
            let position = mesh.Position
            let uv = mesh.Uv
            let color = mesh.Color
            let center = mesh.Center

            let textureBuffer = texture.Buffer
            let programId = shader.ProgramId

            position.TryBufferData () |> ignore
            uv.TryBufferData () |> ignore
            color.TryBufferData () |> ignore
            center.TryBufferData () |> ignore

            let uniformProjection = Renderer.getUniformLocation programId "uni_projection"

            Renderer.setUniformMatrix4x4 uniformProjection projection

            let uniformView = Renderer.getUniformLocation programId "uni_view"

            Renderer.setUniformMatrix4x4 uniformView view

            position.Bind ()
            Renderer.bindPosition programId

            uv.Bind ()
            Renderer.bindUv programId

            color.Bind ()
            Renderer.bindColor programId

            center.Bind ()
            Renderer.bindCenter programId

            Renderer.setTexture programId textureBuffer.Id
            textureBuffer.Bind ()

            Renderer.drawTriangles 0 position.Length

            drawCalls <- drawCalls + 1
        )

        Renderer.disableDepth ()

        //printfn "Draw Calls: %A" drawCalls
