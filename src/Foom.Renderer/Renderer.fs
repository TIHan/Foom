namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.IO

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

type CompactManager<'T> =
    {
        mutable nextId: int
        nextIdQueue: Queue<int>
        dataIndexLookup: int []

        dataIds: int ResizeArray
        data: 'T ResizeArray
    }

    static member Create (maxSize) =
        {
            nextId = 0
            nextIdQueue = Queue ()
            dataIndexLookup = Array.init maxSize (fun _ -> -1)
            dataIds = ResizeArray ()
            data = ResizeArray ()
        }

    member this.Count =
        this.data.Count

    member this.Add datum =
        let id =
            if this.nextIdQueue.Count > 0 then
                this.nextIdQueue.Dequeue ()
            else
                let id = this.nextId
                this.nextId <- this.nextId + 1
                id

        let index = this.data.Count

        this.dataIds.Add id
        this.data.Add datum

        this.dataIndexLookup.[id] <- index

        id

    member this.RemoveById id =

        this.nextIdQueue.Enqueue id

        let index = this.dataIndexLookup.[id]

        let lastIndex = this.data.Count - 1
        let lastId = this.dataIds.[lastIndex]

        this.dataIds.[index] <- this.dataIds.[lastIndex]
        this.data.[index] <- this.data.[lastIndex]

        this.dataIds.RemoveAt (lastIndex)
        this.data.RemoveAt (lastIndex)

        this.dataIndexLookup.[id] <- -1
        this.dataIndexLookup.[lastId] <- index 

    member this.IsValid id =

        if id < this.dataIndexLookup.Length then

            let index = this.dataIndexLookup.[id]

            if index <> -1 then
                true
            else
                false

        else
            false    

    member this.FindById id =

        if id < this.dataIndexLookup.Length then

            let index = this.dataIndexLookup.[id]

            if index <> -1 then
                this.data.[index]
            else
                failwith "Unable to find datum with id, %i." id

        else
            failwith "Not a valid id, %i." id

    member this.ForEach f =

        for i = 0 to this.data.Count - 1 do
            
            f this.dataIds.[i] this.data.[i]

type RenderLayer =
    {
        shaders: Dictionary<ShaderId, ShaderProgram * (Matrix4x4 -> Matrix4x4 -> unit)>
        textureMeshes: Dictionary<ShaderId, Dictionary<TextureId, Texture * Bucket>> 
        mutable nextShaderId: int
    }

    member this.CreateShader (vertexShader, fragmentShader, f: ShaderId -> ShaderProgram -> (Matrix4x4 -> Matrix4x4 -> unit)) =
        let shaderProgram =
            Backend.loadShaders vertexShader fragmentShader
            |> ShaderProgram.Create

        let shaderId = this.nextShaderId

        this.nextShaderId <- this.nextShaderId + 1
        this.shaders.[shaderId] <- (shaderProgram, (f shaderId shaderProgram))

        {
            Id = shaderId
        }

type RenderLayerId =
    {
        id: int
    }

type RenderCamera =
    {
        renderTexture: RenderTexture
        renderLayerId: RenderLayerId
    }

type RenderCameraId =
    {
        id: int
    }


type Renderer =
    {
        mutable nextShaderId: int
        Shaders: Dictionary<ShaderId, ShaderProgram * (Matrix4x4 -> Matrix4x4 -> unit)>
        TextureMeshes: Dictionary<ShaderId, Dictionary<TextureId, Texture * Bucket>> 

        finalRenderTexture: RenderTexture
        finalShaderProgram: ShaderProgram
        finalPositionBuffer: Vector3Buffer
        finalPosition: VertexAttribute<Vector3Buffer>
        finalTexture: Uniform<RenderTexture>
        finalTime: Uniform<float32>


        layerManager: CompactManager<RenderLayer>
        cameraManager: CompactManager<RenderCamera>
    }

    static member Create () =
        let vertexBytes = File.ReadAllText ("Fullscreen.vert") |> System.Text.Encoding.UTF8.GetBytes
        let fragmentBytes = File.ReadAllText ("Fullscreen.frag") |> System.Text.Encoding.UTF8.GetBytes
        let shaderProgram = ShaderProgram.Create (Backend.loadShaders vertexBytes fragmentBytes)

        let vertices =
            [|
                Vector3 (-1.f,-1.f, 0.f)
                Vector3 (1.f, -1.f, 0.f)
                Vector3 (1.f, 1.f, 0.f)
                Vector3 (1.f, 1.f, 0.f)
                Vector3 (-1.f,  1.f, 0.f)
                Vector3 (-1.f, -1.f, 0.f)
            |]

        let positionBuffer = Vector3Buffer (vertices)

        let position = shaderProgram.CreateVertexAttributeVector3 ("position")
        let tex = shaderProgram.CreateUniformRenderTexture ("uni_texture")
        let time = shaderProgram.CreateUniformFloat ("time")
        {
            nextShaderId = 0
            Shaders = Dictionary ()
            TextureMeshes = Dictionary ()
            finalRenderTexture = RenderTexture (1280, 720)
            finalShaderProgram = shaderProgram
            finalPositionBuffer = positionBuffer
            finalPosition = position
            finalTexture = tex
            finalTime = time

            layerManager = CompactManager<RenderLayer>.Create (256)
            cameraManager = CompactManager<RenderCamera>.Create (256)
        }

    member this.CreateVector2Buffer (data) =
        Vector2Buffer (data)

    member this.CreateVector3Buffer (data) =
        Vector3Buffer (data)

    member this.CreateVector4Buffer (data) =
        Vector4Buffer (data)

    member this.CreateTexture2DBuffer (bmp) =
        let buffer = Texture2DBuffer ()
        buffer.Set bmp
        buffer

    member this.CreateShader (vertexShader, fragmentShader, f: ShaderId -> ShaderProgram -> (Matrix4x4 -> Matrix4x4 -> unit)) =
        let shaderProgram =
            Backend.loadShaders vertexShader fragmentShader
            |> ShaderProgram.Create

        let shaderId = this.nextShaderId

        this.nextShaderId <- this.nextShaderId + 1
        this.Shaders.[shaderId] <- (shaderProgram, (f shaderId shaderProgram))

        {
            Id = shaderId
        }

    member this.TryCreateRenderCamera (renderLayerId: RenderLayerId) =
        if this.layerManager.IsValid renderLayerId.id then

            let renderCamera =
                {
                    renderTexture = RenderTexture (1280, 720)
                    renderLayerId = renderLayerId
                }

            let renderCameraId =
                {
                    RenderCameraId.id = this.cameraManager.Add renderCamera
                }

            Some renderCameraId
        else
            None

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

                            in_texture.Set texture.Buffer

                            for i = 0 to count - 1 do

                                let mesh = bucket.Meshes.[i]
                                let o = bucket.Data.[i]

                                in_position.Set     mesh.Position
                                in_uv.Set           mesh.Uv
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
        let buffer = Texture2DBuffer ()
        buffer.Set bmp
        {
            Buffer = buffer
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

    member this.Draw (time: float32) (projection: Matrix4x4) (view: Matrix4x4) =            

        this.finalRenderTexture.TryBufferData () |> ignore

        this.finalRenderTexture.BindFramebuffer ()

        Backend.clear ()
        
        Backend.enableDepth ()

        this.Shaders
        |> Seq.iter (fun pair ->
            (snd pair.Value) view projection
        )

        Backend.disableDepth ()

        this.finalRenderTexture.Render ()

        Backend.clear ()

        Backend.useProgram this.finalShaderProgram.programId

        this.finalPosition.Set this.finalPositionBuffer
        this.finalTexture.Set this.finalRenderTexture
        this.finalTime.Set time

        this.finalShaderProgram.Run ()

// *****************************************
// *****************************************