namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.IO

open Foom.Collections

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

        let positionBuffer = Buffer.createVector3 vertices

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

    member this.CreateShader (vertexShader, fragmentShader, f: ShaderId -> ShaderProgram -> (Matrix4x4 -> Matrix4x4 -> Texture -> Bucket -> unit)) =
        let shaderProgram =
            Backend.loadShaders vertexShader fragmentShader
            |> ShaderProgram.Create

        let shaderId = this.nextShaderId

        this.nextShaderId <- this.nextShaderId + 1
        this.shaders.[shaderId] <- (shaderProgram, (f shaderId shaderProgram))

        shaderId

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

                let run renderPass = shaderProgram.Run renderPass

                fun view projection texture bucket ->
                    let count = bucket.meshes.Count

                    in_texture.Set      texture.Buffer
                    uni_view.Set        view
                    uni_projection.Set  projection

                    for i = 0 to count - 1 do

                        let mesh, o = bucket.meshes.[i]

                        in_position.Set     mesh.Position
                        in_uv.Set           mesh.Uv

                        let color = mesh.Color

                        let programId = shaderProgram.programId

                        color.TryBufferData () |> ignore

                        color.Bind ()
                        Backend.bindColor programId

                        update o run

        )

    member this.TryAdd (shaderId: ShaderId, texture: Texture, mesh: Mesh, data: obj, renderLayerIndex: int) =

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

        match this.layerManager.TryFindById (CompactId (renderLayerIndex, 1u)) with
        | Some renderLayer ->

            let bucketLookup =
                match renderLayer.textureMeshes.TryGetValue (shaderId) with
                | true, (bucketLookup) -> bucketLookup
                | _, _ ->
                    let bucketLookup = Dictionary ()
                    renderLayer.textureMeshes.Add (shaderId, bucketLookup)
                    bucketLookup

            let bucket = addTexture bucketLookup texture

            bucket.Add (mesh, data)

            // TODO: Need to handle mesh ids.

            true

        | _ -> false

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

                renderCamera.renderTexture.Unbind ()

        match this.mainRenderCameraId with
        | Some renderCameraId ->

            let renderCamera = this.cameraManager.FindById renderCameraId.id

            Backend.clear ()

            Backend.useProgram this.finalShaderProgram.programId


            this.finalPosition.Set this.finalPositionBuffer
            this.finalTexture.Set renderCamera.renderTexture
            this.finalTime.Set time

            this.finalShaderProgram.Run RenderPass.Depth

            Backend.useProgram 0

        | _ -> ()

// *****************************************
// *****************************************