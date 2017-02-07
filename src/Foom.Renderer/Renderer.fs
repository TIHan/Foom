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
        meshes: (Mesh * obj) ResizeArray
    }

    member this.Add (mesh, o) =
        this.meshes.Add ((mesh, o))

type ShaderId = int
type TextureId = int

type Shader =
    {
        Id: int
    }

type Material =
    {
        Shader: Shader
        Texture: Texture
    }

type CompactId =

    val Index : int

    val Version : uint32

    new (index, version) = { Index = index; Version = version }

type CompactManager<'T> =
    {
        mutable nextIndex: int

        maxSize: int
        versions: uint32 []
        nextIndexQueue: Queue<int>
        dataIndexLookup: int []

        dataIds: CompactId ResizeArray
        data: 'T ResizeArray
    }

    static member Create (maxSize) =
        {
            nextIndex = 0
            maxSize = maxSize
            versions = Array.init maxSize (fun _ -> 1u)
            nextIndexQueue = Queue ()
            dataIndexLookup = Array.init maxSize (fun _ -> -1)
            dataIds = ResizeArray ()
            data = ResizeArray ()
        }

    member this.Count =
        this.data.Count

    member this.Add datum =
        if this.nextIndex >= this.maxSize then
            printfn "Unable to add datum. Reached max size, %i." this.maxSize
            CompactId (0, 0u)
        else

        let id =
            if this.nextIndexQueue.Count > 0 then
                let index = this.nextIndexQueue.Dequeue ()
                let version = this.versions.[index] + 1u
                CompactId (index, version)
            else
                let index = this.nextIndex
                this.nextIndex <- this.nextIndex + 1
                CompactId (index, 1u)

        let index = this.dataIds.Count

        this.dataIds.Add id
        this.data.Add datum

        this.dataIndexLookup.[id.Index] <- index
        this.versions.[id.Index] <- id.Version

        id

    member this.RemoveById (id: CompactId) =
        if this.IsValid id then

            this.nextIndexQueue.Enqueue id.Index

            let index = this.dataIndexLookup.[id.Index]

            let lastIndex = this.data.Count - 1
            let lastId = this.dataIds.[lastIndex]

            this.dataIds.[index] <- this.dataIds.[lastIndex]
            this.data.[index] <- this.data.[lastIndex]

            this.dataIds.RemoveAt (lastIndex)
            this.data.RemoveAt (lastIndex)

            this.dataIndexLookup.[id.Index] <- -1
            this.dataIndexLookup.[lastId.Index] <- index 

        else
            failwithf "Not a valid id, %A." id

    member this.IsValid (id: CompactId) =

        if id.Index < this.dataIndexLookup.Length && id.Version = this.versions.[id.Index] then

            let index = this.dataIndexLookup.[id.Index]

            index <> -1

        else
            false    

    member this.FindById (id: CompactId) =

        if this.IsValid id then

            let index = this.dataIndexLookup.[id.Index]

            if index <> -1 then
                this.data.[index]
            else
                failwithf "Unable to find datum with id, %A." id

        else
            failwithf "Not a valid id, %A." id

    member this.TryFindById (id: CompactId) =

        if this.IsValid id then
            let index = this.dataIndexLookup.[id.Index]

            if index <> -1 then
                Some this.data.[index]
            else
                None
        else
            None

    member this.ForEach f =

        for i = 0 to this.data.Count - 1 do
            
            f this.dataIds.[i] this.data.[i]

type RenderCamera =
    {
        mutable view: Matrix4x4
        mutable projection: Matrix4x4
        depth: int
        renderTexture: RenderTexture
        renderLayerIndex: int
    }

type RenderCameraId =
    {
        id: CompactId
    }

type RenderLayer =
    {
        textureMeshes: Dictionary<ShaderId, Dictionary<TextureId, Texture * Bucket>> 
    }

    static member Create () =
        {
            textureMeshes = Dictionary ()
        }

type Renderer =
    {
        mutable nextShaderId: int
        shaders: Dictionary<ShaderId, ShaderProgram * (Matrix4x4 -> Matrix4x4 -> Texture -> Bucket -> unit)>

        finalShaderProgram: ShaderProgram
        finalPositionBuffer: Vector3Buffer
        finalPosition: VertexAttribute<Vector3Buffer>
        finalTexture: Uniform<RenderTexture>
        finalTime: Uniform<float32>


        layerManager: CompactManager<RenderLayer>
        cameraManager: CompactManager<RenderCamera>
        renderCameraDepths: RenderCameraId ResizeArray []
        mutable mainRenderCameraId: RenderCameraId option
    }

    static member Create () =
        let maxRenderCameraDepth = 16
        let maxRenderCameras = 16
        let maxRenderLayers = 16

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


        let layerManager = CompactManager<RenderLayer>.Create maxRenderLayers

        for i = 1 to 16 do
            layerManager.Add (RenderLayer.Create ()) |> ignore

        {
            nextShaderId = 0
            shaders = Dictionary ()
            finalShaderProgram = shaderProgram
            finalPositionBuffer = positionBuffer
            finalPosition = position
            finalTexture = tex
            finalTime = time

            layerManager = layerManager
            cameraManager = CompactManager<RenderCamera>.Create maxRenderCameras
            renderCameraDepths = Array.init maxRenderCameraDepth (fun _ -> ResizeArray ())
            mainRenderCameraId = None
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

    member this.TryCreateRenderCamera view projection renderLayerIndex depth =
        if this.layerManager.IsValid (CompactId (renderLayerIndex, 1u)) && depth < this.renderCameraDepths.Length then

            let renderCamera =
                {
                    view = view
                    projection = projection
                    depth = depth
                    renderTexture = RenderTexture (1280, 720)
                    renderLayerIndex = renderLayerIndex
                }

            let renderCameraId =
                {
                    RenderCameraId.id = this.cameraManager.Add renderCamera
                }

            this.renderCameraDepths.[depth].Add renderCameraId

            if this.mainRenderCameraId.IsNone then
                this.mainRenderCameraId <- Some renderCameraId

            Some renderCamera
        else
            None

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

    member this.CreateShader (vertexShader, fragmentShader, f: ShaderId -> ShaderProgram -> (Matrix4x4 -> Matrix4x4 -> Texture -> Bucket -> unit)) =
        let shaderProgram =
            Backend.loadShaders vertexShader fragmentShader
            |> ShaderProgram.Create

        let shaderId = this.nextShaderId

        this.nextShaderId <- this.nextShaderId + 1
        this.shaders.[shaderId] <- (shaderProgram, (f shaderId shaderProgram))

        {
            Id = shaderId
        }

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

                fun view projection texture bucket ->
                    let count = bucket.meshes.Count

                    in_texture.Set      texture.Buffer
                    uni_view.Set        view
                    uni_projection.Set  projection

                    for i = 0 to count - 1 do

                        let mesh, o = bucket.meshes.[i]

                        in_position.Set     mesh.Position
                        in_uv.Set           mesh.Uv

                        if isNull o |> not then update o

                        let color = mesh.Color

                        let programId = shaderProgram.programId

                        color.TryBufferData () |> ignore

                        color.Bind ()
                        Backend.bindColor programId

                        shaderProgram.Run ()

        )

    member this.TryAdd (material: Material, mesh: Mesh, data: obj, renderLayerIndex: int) =

        let addTexture (bucketLookup: Dictionary<TextureId, Texture * Bucket>) texture = 
            // Need to do this to make sure we get an id.
            texture.Buffer.TryBufferData () |> ignore

            let bucket =
                match bucketLookup.TryGetValue (texture.Buffer.Id) with
                | true, (_, bucket) -> bucket
                | _ ->
                    let bucket =
                        {
                            meshes = ResizeArray ()
                        }

                    bucketLookup.Add (texture.Buffer.Id, (texture, bucket))
                    bucket
            bucket

        let add shader =
            match this.layerManager.TryFindById (CompactId (renderLayerIndex, 1u)) with
            | Some renderLayer ->

                let bucketLookup =
                    match renderLayer.textureMeshes.TryGetValue (shader.Id) with
                    | true, (bucketLookup) -> bucketLookup
                    | _, _ ->
                        let bucketLookup = Dictionary ()
                        renderLayer.textureMeshes.Add (shader.Id, bucketLookup)
                        bucketLookup

                let bucket = addTexture bucketLookup material.Texture

                bucket.Add (mesh, data)

                // TODO: Need to handle mesh ids.

                true

            | _ -> false

        add material.Shader

    member this.Draw (time: float32) =

        for i = 0 to this.renderCameraDepths.Length - 1 do

            let renderCameraIds = this.renderCameraDepths.[i]

            for i = 0 to renderCameraIds.Count - 1 do

                let renderCameraId = renderCameraIds.[i]

                let renderCamera = this.cameraManager.FindById renderCameraId.id

                let renderLayer = this.layerManager.FindById (CompactId (renderCamera.renderLayerIndex, 1u))

                let renderCamera = this.cameraManager.FindById renderCameraId.id

                renderCamera.renderTexture.TryBufferData () |> ignore

                renderCamera.renderTexture.Bind ()

                Backend.clear ()

                Backend.enableDepth ()

                renderLayer.textureMeshes
                |> Seq.iter (fun pair ->
                    let shaderId = pair.Key
                    let textureLookup = pair.Value

                    let shader, f = this.shaders.[shaderId]

                    Backend.useProgram shader.programId

                    textureLookup
                    |> Seq.iter (fun pair ->
                        let texture, bucket = pair.Value
                        f renderCamera.view renderCamera.projection texture bucket
                    )

                    Backend.useProgram 0
                )

                Backend.disableDepth ()

                renderCamera.renderTexture.Unbind ()

        match this.mainRenderCameraId with
        | Some renderCameraId ->

            let renderCamera = this.cameraManager.FindById renderCameraId.id

            Backend.clear ()

            Backend.useProgram this.finalShaderProgram.programId


            this.finalPosition.Set this.finalPositionBuffer
            this.finalTexture.Set renderCamera.renderTexture
            this.finalTime.Set time

            this.finalShaderProgram.Run ()

            Backend.useProgram 0

        | _ -> ()

// *****************************************
// *****************************************