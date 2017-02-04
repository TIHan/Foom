namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

// *****************************************
// *****************************************
// Renderer
// *****************************************
// *****************************************

type Texture =
    {
        Buffer: Texture2DBuffer
    }

type Mesh =
    {
        Position: Vector3Buffer
        Uv: Vector2Buffer
        Color: Vector4Buffer
    }

[<ReferenceEquality>]
type Bucket =
    {
        Meshes: Mesh ResizeArray
        Data: obj ResizeArray
        IdRefs: int Ref ResizeArray
    }

    member this.Add (mesh: Mesh, data: obj) =
        let idRef = ref this.IdRefs.Count

        this.Meshes.Add (mesh)
        this.Data.Add (data)
        this.IdRefs.Add (idRef)

        idRef

    member this.RemoveById id =
        let lastIndex = this.IdRefs.Count - 1

        this.Meshes.[id] <- this.Meshes.[lastIndex]
        this.Data.[id] <- this.Data.[lastIndex]
        this.IdRefs.[id] <- this.IdRefs.[lastIndex]
        this.IdRefs.[id] := id

        this.Meshes.RemoveAt (lastIndex)
        this.Data.RemoveAt (lastIndex)
        this.IdRefs.RemoveAt (lastIndex)

type ShaderId = int
type TextureId = int

type TextureMeshId =
    {
        ShaderId: int
        TextureId: int
        IdRef: int ref
    }

type Shader =
    {
        Id: int
    }

type Material =
    {
        Shader: Shader
        Texture: Texture
    }

type Renderer =
    {
        mutable nextShaderId: int
        Shaders: Dictionary<ShaderId, Matrix4x4 -> Matrix4x4 -> unit>
        TextureMeshes: Dictionary<ShaderId, Dictionary<TextureId, Texture * Bucket>> 
    }

    static member Create () =
        {
            nextShaderId = 0
            Shaders = Dictionary ()
            TextureMeshes = Dictionary ()
        }

    member this.CreateVector2Buffer (data) =
        Vector2Buffer (data)

    member this.CreateVector3Buffer (data) =
        Vector3Buffer (data)

    member this.CreateVector4Buffer (data) =
        Vector4Buffer (data)

    member this.CreateTexture2DBuffer (bmp) =
        Texture2DBuffer (bmp)

    member this.CreateShader (vertexShader, fragmentShader, f: ShaderId -> ShaderProgram -> (Matrix4x4 -> Matrix4x4 -> unit)) =
        let shaderProgram =
            Backend.loadShaders vertexShader fragmentShader
            |> ShaderProgram.Create

        let shaderId = this.nextShaderId

        this.nextShaderId <- this.nextShaderId + 1
        this.Shaders.[shaderId] <- f shaderId shaderProgram

        {
            Id = shaderId
        }

    // TextureMesh shader
    member this.CreateTextureMeshShader (vertexShader, fragmentShader, f) =
        this.CreateShader (vertexShader, fragmentShader,

            fun shaderId shaderProgram ->
                (*
                    // Ideal
                    shaderInput {
                        let! in_position = in_vec3 "position"
                        let! in_uv = in_vec2 "in_uv"
                        let! in_texture = in_sampler2D "uni_texture"
                        let! uni_view = uni_mat4x4 "uni_view"
                        let! uni_projection = uni_mat4x4 "uni_projection"

                        fun view projection ->
                            ....
                    }
                *)
                let in_position = shaderProgram.CreateVertexAttributeVector3 ("position")
                let in_uv = shaderProgram.CreateVertexAttributeVector2 ("in_uv")
                let in_texture = shaderProgram.CreateUniformTexture2D ("uni_texture")
                let uni_view = shaderProgram.CreateUniformMatrix4x4 ("uni_view")
                let uni_projection = shaderProgram.CreateUniformMatrix4x4 ("uni_projection")

                let update = f shaderProgram

                fun view projection ->
                    match this.TextureMeshes.TryGetValue(shaderId) with
                    | true, textureLookup ->

                        Backend.useProgram shaderProgram.programId

                        textureLookup
                        |> Seq.iter (fun pair ->
                            let textureId = pair.Key
                            let (texture, bucket) = pair.Value

                            let count = bucket.Meshes.Count

                            for i = 0 to count - 1 do

                                let mesh = bucket.Meshes.[i]
                                let o = bucket.Data.[i]

                                in_position.Set     mesh.Position
                                in_uv.Set           mesh.Uv
                                in_texture.Set      texture.Buffer
                                uni_view.Set        view
                                uni_projection.Set  projection

                                if o <> null then
                                    update o

                                let color = mesh.Color

                                let programId = shaderProgram.programId

                                color.TryBufferData () |> ignore

                                color.Bind ()
                                Backend.bindColor programId

                                shaderProgram.Run ()
                        )
                    | _ -> ()

        )

    member this.CreateTexture (bmp) =
        {
            Buffer = Texture2DBuffer (bmp)
        }

    member this.CreateMaterial (shader, texture) =
        {
            Shader = shader
            Texture = texture
        }

    member this.CreateMesh (position, uv, color: Color []) =
        {
            Position = Vector3Buffer (position)
            Uv = Vector2Buffer (uv)
            Color =
                color
                |> Array.map (fun c ->
                    Vector4 (
                        single c.R / 255.f,
                        single c.G / 255.f,
                        single c.B / 255.f,
                        single c.A / 255.f)
                )
                |> Vector4Buffer
        }

    member this.TryAdd (material: Material, mesh: Mesh, data: obj) =

        let addTexture (bucketLookup: Dictionary<TextureId, Texture * Bucket>) texture = 
            // Need to do this to make sure we get an id.
            texture.Buffer.TryBufferData () |> ignore

            let bucket =
                match bucketLookup.TryGetValue (texture.Buffer.Id) with
                | true, (_, bucket) -> bucket
                | _ ->
                    let bucket =
                        {
                            Meshes = ResizeArray ()
                            Data = ResizeArray ()
                            IdRefs = ResizeArray ()
                        }

                    bucketLookup.Add (texture.Buffer.Id, (texture, bucket))
                    bucket
            bucket

        let add shader =
            let bucketLookup =
                match this.TextureMeshes.TryGetValue (shader.Id) with
                | true, (bucketLookup) -> bucketLookup
                | _, _ ->
                    let bucketLookup = Dictionary ()
                    this.TextureMeshes.Add (shader.Id, bucketLookup)
                    bucketLookup

            let bucket = addTexture bucketLookup material.Texture

            let idRef = bucket.Add (mesh, data)

            let render =
                {
                    ShaderId = shader.Id
                    TextureId = material.Texture.Buffer.Id
                    IdRef = idRef
                }

            Some render

        add material.Shader

    member this.Clear () =
        Backend.clear ()

    member this.Draw (projection: Matrix4x4) (view: Matrix4x4) =
        
        Backend.enableDepth ()

        this.Shaders
        |> Seq.iter (fun pair ->
            pair.Value view projection
        )

        Backend.disableDepth ()

// *****************************************
// *****************************************