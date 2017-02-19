namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.IO

open Foom.Collections

// *****************************************
// *****************************************
// Texture
// *****************************************
// *****************************************

type Texture =
    {
        Buffer: Texture2DBuffer
    }

// *****************************************
// *****************************************
// Cache
// *****************************************
// *****************************************

type ShaderProgramCache () =
    let cache = Dictionary<string, ShaderProgram> ()

    member this.GetOrCreateShader (name: string) =
        let name = name.ToUpper()

        match cache.TryGetValue (name) with
        | true, shader -> shader
        | _ ->
            let shaderProgram = ShaderProgram.Load (name)

            cache.[name] <- shaderProgram

            shaderProgram

    member this.Remove (shader) =
        cache.Remove (shader.name)

type TextureCache () =
    let cache = Dictionary<string, Texture> ()

    member this.GetOrCreateTexture (fileName: string) =
        let fileName = fileName.ToUpper ()

        match cache.TryGetValue (fileName) with
        | true, texture -> texture
        | _ ->
            let bmp = new Bitmap(fileName)

            let buffer = Texture2DBuffer ([||], 0, 0)

            // TODO: we should extract the bmp logic from Texture2DBuffer and put it here.
            buffer.Set bmp

            buffer.TryBufferData () |> ignore

            {
                Buffer = buffer
            }

// *****************************************
// *****************************************
// Shader
// *****************************************
// *****************************************

type Shader<'Input, 'Output> = Shader of 'Input * 'Output * ShaderProgram

[<Sealed>]
type PipelineContext (shaderProgramCache: ShaderProgramCache) =
    
    let releases = ResizeArray<unit -> unit> ()
    let actions = ResizeArray<unit -> unit> ()

    member this.AddRelease release =
        releases.Add release

    member this.AddAction action =
        actions.Add action

type Pipeline<'a> = private Pipeline of (PipelineContext -> 'a)

type PipelineBuilder () =

    member this.Bind (Pipeline x : Pipeline<'a>, f: 'a -> Pipeline<'b>) : Pipeline<'b> = 
        Pipeline (
            fun context ->
                match f (x context) with
                | Pipeline g -> g context
        )

    member this.Delay (f: unit -> Pipeline<'a>) : Pipeline<'a> = 
        Pipeline (fun context -> match f () with | Pipeline x -> x context)

    member this.Return (x: 'a) : Pipeline<'a> =
        Pipeline (fun _ -> x)

    member this.Zero () : Pipeline<unit> =
        Pipeline (fun _ -> ())

type Shader<'Input, 'Output> with

    member this.Run f =
        match this with
        | Shader (input, output, program) ->

            Pipeline (
                fun _ ->
                    f input program.Draw
                    output
            )

module Pipeline =

    let pipeline = PipelineBuilder ()

    let run context p =
        match p with
        | Pipeline f -> f context

    let clear =
        Pipeline (
            fun _ ->
                Backend.clear ()
        )

    let captureFrame p =
        let renderTexture (context: PipelineContext) =
            lazy
                let renderTexture = RenderTexture (1280, 720)
                context.AddRelease renderTexture.Release
                renderTexture

        Pipeline (
            fun context ->
                let renderTexture = (renderTexture context).Force()
                
                match p with
                | Pipeline f -> f context

                renderTexture
        )

open Pipeline

// *****************************************
// *****************************************
// Final Output Program
// *****************************************
// *****************************************

module Final =

    [<Sealed>]
    type FinalInput (shaderProgram: ShaderProgram) =

        member val Time = shaderProgram.CreateUniformFloat ("time")

        member val RenderTexture = shaderProgram.CreateUniformRenderTexture ("uni_texture")

        member val Position = shaderProgram.CreateVertexAttributeVector3 ("position")

    let finalPipeline worldPipeline (getTime: unit -> float32) (getPosition: unit -> Vector3Buffer) =
        pipeline {
            let finalShader = Unchecked.defaultof<Shader<FinalInput, unit>>

            let! renderTexture = captureFrame worldPipeline

            do! finalShader.Run (fun input draw ->
                input.Time.Set (getTime ())
                input.Position.Set (getPosition ())
                input.RenderTexture.Set renderTexture

                draw ()
            )
        }

// *****************************************
// *****************************************
// Mesh
// *****************************************
// *****************************************

[<Sealed>]
type Mesh (position, uv, color) =

    member val Position = Buffer.createVector3 position

    member val Uv = Buffer.createVector2 uv

    member val Color = Buffer.createVector4 color

// *****************************************
// *****************************************
// Renderer
// *****************************************
// *****************************************

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
type CameraLayerFlags =
    | None =    0b0000000
    | Layer0 =  0b0000001
    | Layer1 =  0b0000010
    | Layer2 =  0b0000100
    | Layer3 =  0b0001000
    | Layer4 =  0b0010000
    | Layer5 =  0b0100000
    | Layer6 =  0b1000000
    | All =     0b1111111

type CameraLayerIndex =
    | Layer0 =  0b0000001
    | Layer1 =  0b0000010
    | Layer2 =  0b0000100
    | Layer3 =  0b0001000
    | Layer4 =  0b0010000
    | Layer5 =  0b0100000
    | Layer6 =  0b1000000

[<Flags>]
type CameraClearFlags =
    | None =    0b0000000
    | Depth =   0b0000001
    | Color =   0b0000010
    | Stencil = 0b0000100
    | All =     0b0000111

type Camera =
    {
        mutable id: CompactId
        mutable view: Matrix4x4
        mutable projection: Matrix4x4
        depth: int
        layerFlags: CameraLayerFlags
        clearFlags: CameraClearFlags
    }

type CameraSettings =
    {
        projection: Matrix4x4
        depth: int
        layerFlags: CameraLayerFlags
        clearFlags: CameraClearFlags
    }

type CameraLayer =
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


        layerManager: CompactManager<CameraLayer>
        cameraManager: CompactManager<Camera>
        cameraDepths: Camera ResizeArray []
    }

    static member Create () =
        let maxCameraDepth = 100
        let maxCameras = 100
        let maxCameraLayers = 7

        let shaderProgram = ShaderProgram.Load("Fullscreen")

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


        let layerManager = CompactManager<CameraLayer>.Create maxCameraLayers

        for i = 1 to maxCameraLayers do
            layerManager.Add (CameraLayer.Create ()) |> ignore

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
            cameraManager = CompactManager<Camera>.Create maxCameras
            cameraDepths = Array.init maxCameraDepth (fun _ -> ResizeArray ())
        }

    member this.TryCreateRenderCamera settings =
        if settings.depth < this.cameraDepths.Length && not this.cameraManager.IsFull then

            let camera =
                {
                    id = CompactId.Zero
                    view = Matrix4x4.Identity
                    projection = settings.projection
                    depth = settings.depth
                    layerFlags = settings.layerFlags
                    clearFlags = settings.clearFlags
                }

            camera.id <- this.cameraManager.Add camera

            // TODO: How do we handle removal here?
            this.cameraDepths.[settings.depth].Add camera

            Some camera
        else
            None

    member this.CreateShader (name, drawOperation, f: ShaderId -> ShaderProgram -> (float32 -> Matrix4x4 -> Matrix4x4 -> Texture -> Bucket -> unit)) =
        let shaderProgram = ShaderProgram.Load (name)

        let shaderId = this.nextShaderId

        this.nextShaderId <- this.nextShaderId + 1
        this.shaders.[shaderId] <- (shaderProgram, (f shaderId shaderProgram))

        shaderId

    member this.CreateTextureMeshShader (name, drawOperation, f) =
        this.CreateShader (name, drawOperation,

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

        for i = 0 to this.cameraDepths.Length - 1 do

            let cameras = this.cameraDepths.[i]

            for i = 0 to cameras.Count - 1 do

                let camera = cameras.[i]

                //renderCamera.renderTexture.TryBufferData () |> ignore

                //renderCamera.renderTexture.Bind ()

                if camera.clearFlags.HasFlag (CameraClearFlags.Depth) then
                    Backend.clearDepth ()

                if camera.clearFlags.HasFlag (CameraClearFlags.Color) then
                    Backend.clearColor ()

                if camera.clearFlags.HasFlag (CameraClearFlags.Stencil) then
                    Backend.clearStencil ()

                for i = 0 to this.layerManager.Count - 1 do
                    let mask =
                        match i with
                        | 0 -> CameraLayerFlags.Layer0
                        | 1 -> CameraLayerFlags.Layer1
                        | 2 -> CameraLayerFlags.Layer2
                        | 3 -> CameraLayerFlags.Layer3
                        | 4 -> CameraLayerFlags.Layer4
                        | 5 -> CameraLayerFlags.Layer5
                        | 6 -> CameraLayerFlags.Layer6
                        | _ -> CameraLayerFlags.None

                    if camera.layerFlags.HasFlag (mask) then
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
                                    f time camera.view camera.projection texture bucket
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