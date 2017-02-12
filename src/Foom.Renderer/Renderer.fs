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

[<Flags>]
type LayerMask =
    | None =    0b0000000
    | Layer0 =  0b0000001
    | Layer1 =  0b0000010
    | Layer2 =  0b0000100
    | Layer3 =  0b0001000
    | Layer4 =  0b0010000
    | Layer5 =  0b0100000
    | Layer6 =  0b1000000

[<Flags>]
type ClearFlags =
    | None =    0b0000000
    | Depth =   0b0000001
    | Color =   0b0000010
    | Stencil = 0b0000100
    | All =     0b0000111

type RenderCamera =
    {
        mutable view: Matrix4x4
        mutable projection: Matrix4x4
        depth: int
        renderTexture: RenderTexture
        layerMask: LayerMask
        clearFlags: ClearFlags
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
        shaders: Dictionary<ShaderId, ShaderProgram * (float32 -> Matrix4x4 -> Matrix4x4 -> Texture -> Bucket -> unit)>

        finalShaderProgram: ShaderProgram
        finalPositionBuffer: Vector3Buffer
        finalRenderTexture: RenderTexture
        finalPosition: VertexAttribute<Vector3Buffer>
        finalTexture: Uniform<RenderTexture>
        finalTime: Uniform<float32>


        layerManager: CompactManager<RenderLayer>
        cameraManager: CompactManager<RenderCamera>
        renderCameraDepths: RenderCameraId ResizeArray []
    }

    static member Create () =
        let maxRenderCameraDepth = 100
        let maxRenderCameras = 100
        let maxRenderLayers = 7

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

        for i = 1 to maxRenderLayers do
            layerManager.Add (RenderLayer.Create ()) |> ignore

        {
            nextShaderId = 0
            shaders = Dictionary ()
            finalShaderProgram = shaderProgram
            finalRenderTexture = RenderTexture (1280, 720)
            finalPositionBuffer = positionBuffer
            finalPosition = position
            finalTexture = tex
            finalTime = time

            layerManager = layerManager
            cameraManager = CompactManager<RenderCamera>.Create maxRenderCameras
            renderCameraDepths = Array.init maxRenderCameraDepth (fun _ -> ResizeArray ())
        }

    member this.TryCreateRenderCamera view projection layerMask clearFlags depth =
        if depth < this.renderCameraDepths.Length && not this.cameraManager.IsFull then

            let renderCamera =
                {
                    view = view
                    projection = projection
                    depth = depth
                    renderTexture = RenderTexture (1280, 720)
                    layerMask = layerMask
                    clearFlags = clearFlags
                }

            let renderCameraId =
                {
                    RenderCameraId.id = this.cameraManager.Add renderCamera
                }

            this.renderCameraDepths.[depth].Add renderCameraId

            Some renderCamera
        else
            None

    member this.CreateShader (vertexShader, fragmentShader, f: ShaderId -> ShaderProgram -> (float32 -> Matrix4x4 -> Matrix4x4 -> Texture -> Bucket -> unit)) =
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
                let iPosition = shaderProgram.CreateVertexAttributeVector3 ("position")
                let iUv = shaderProgram.CreateVertexAttributeVector2 ("in_uv")
                let uTexture = shaderProgram.CreateUniformTexture2D ("uni_texture")
                let uTextureResolution = shaderProgram.CreateUniformVector2("uTextureResolution")
                let uView = shaderProgram.CreateUniformMatrix4x4 ("uni_view")
                let uProjection = shaderProgram.CreateUniformMatrix4x4 ("uni_projection")
                let uTime = shaderProgram.CreateUniformFloat ("uTime")

                let update = f shaderProgram

                let run renderPass = shaderProgram.Run renderPass

                fun time view projection texture bucket ->
                    let count = bucket.meshes.Count

                    uTexture.Set texture.Buffer
                    uTextureResolution.Set (Vector2 (single texture.Buffer.Width, single texture.Buffer.Height))
                    uView.Set view
                    uProjection.Set projection
                    uTime.Set time

                    for i = 0 to count - 1 do

                        let mesh, o = bucket.meshes.[i]

                        iPosition.Set mesh.Position
                        iUv.Set mesh.Uv

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

        Backend.enableDepth ()

        this.finalRenderTexture.TryBufferData () |> ignore
        this.finalRenderTexture.Bind ()

        for i = 0 to this.renderCameraDepths.Length - 1 do

            let renderCameraIds = this.renderCameraDepths.[i]

            for i = 0 to renderCameraIds.Count - 1 do

                let renderCameraId = renderCameraIds.[i]
                let renderCamera = this.cameraManager.FindById renderCameraId.id

                //renderCamera.renderTexture.TryBufferData () |> ignore

                //renderCamera.renderTexture.Bind ()

                if renderCamera.clearFlags.HasFlag (ClearFlags.Depth) then
                    Backend.clearDepth ()

                if renderCamera.clearFlags.HasFlag (ClearFlags.Color) then
                    Backend.clearColor ()

                if renderCamera.clearFlags.HasFlag (ClearFlags.Stencil) then
                    Backend.clearStencil ()

                for i = 0 to this.layerManager.Count - 1 do
                    let mask =
                        match i with
                        | 0 -> LayerMask.Layer0
                        | 1 -> LayerMask.Layer1
                        | 2 -> LayerMask.Layer2
                        | 3 -> LayerMask.Layer3
                        | 4 -> LayerMask.Layer4
                        | 5 -> LayerMask.Layer5
                        | 6 -> LayerMask.Layer6
                        | _ -> LayerMask.None

                    if renderCamera.layerMask.HasFlag (mask) |> not then
                        this.layerManager.TryFindById (CompactId (i, 1u))
                        |> Option.iter (fun renderLayer ->

                            renderLayer.textureMeshes
                            |> Seq.iter (fun pair ->
                                let shaderId = pair.Key
                                let textureLookup = pair.Value

                                let shader, f = this.shaders.[shaderId]

                                Backend.useProgram shader.programId

                                textureLookup
                                |> Seq.iter (fun pair ->
                                    let texture, bucket = pair.Value
                                    f time renderCamera.view renderCamera.projection texture bucket
                                    shader.Unbind ()
                                )

                                Backend.useProgram 0
                            )
                        )

                //renderCamera.renderTexture.Unbind ()

        //Backend.disableStencilTest ()
        this.finalRenderTexture.Unbind ()


        Backend.clear ()

        Backend.useProgram this.finalShaderProgram.programId


        this.finalPosition.Set this.finalPositionBuffer
        this.finalTexture.Set this.finalRenderTexture
        this.finalTime.Set time

        this.finalShaderProgram.Run RenderPass.Depth
        this.finalShaderProgram.Unbind ()

        Backend.useProgram 0

        Backend.disableDepth ()

// *****************************************
// *****************************************