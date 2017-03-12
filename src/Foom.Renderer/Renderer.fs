namespace Foom.Renderer

open System
open System.Numerics
open System.Collections.Generic
open System.IO

open Foom.Collections

// *****************************************
// *****************************************
// Mesh
// *****************************************
// *****************************************

[<AbstractClass>]
type GpuResource () =

    member val OwnerCount = 0 with get, set

[<Sealed>]
type UnitResource () =
    inherit GpuResource ()

[<Sealed>]
type Mesh (position, uv, color) =
    inherit GpuResource ()

    member val Position = Buffer.createVector3 position

    member val Uv = Buffer.createVector2 uv

    member val Color = Buffer.createVector4 color

type MeshInput (shaderProgram: ShaderProgram) =
    
    member val Position = shaderProgram.CreateVertexAttributeVector3 ("position")

    member val Uv = shaderProgram.CreateVertexAttributeVector2 ("in_uv")

    member val Color = shaderProgram.CreateVertexAttributeVector4 ("in_color")

    member val Texture = shaderProgram.CreateUniformTexture2D ("uni_texture")

    member val View = shaderProgram.CreateUniformMatrix4x4 ("uni_view")

    member val Projection = shaderProgram.CreateUniformMatrix4x4 ("uni_projection")

    member val Time = shaderProgram.CreateUniformFloat ("uTime")

    member val TextureResolution = shaderProgram.CreateUniformVector2 ("uTextureResolution")

// *****************************************
// *****************************************
// Cache
// *****************************************
// *****************************************

type ProgramCache (gl: IGL, fileReadAllText) =
    let cache = Dictionary<string, int> ()

    member this.CreateShaderProgram (name: string) =
        let name = name.ToUpper()

        let programId =
            match cache.TryGetValue (name) with
            | true, programId -> programId
            | _ ->
                let vertexBytes = fileReadAllText (name + ".vert")//File.ReadAllText (name + ".vert") |> System.Text.Encoding.UTF8.GetBytes
                let fragmentBytes = fileReadAllText (name + ".frag")//File.ReadAllText (name + ".frag") |> System.Text.Encoding.UTF8.GetBytes
                let programId = gl.LoadProgram (vertexBytes, fragmentBytes)//Backend.loadShaders vertexBytes fragmentBytes

                cache.[name] <- programId

                programId

        ShaderProgram.Create (gl, programId)

    member this.Remove (name: string) =
        cache.Remove (name.ToUpper ())

// *****************************************
// *****************************************
// Shader
// *****************************************
// *****************************************

[<Struct>]
type TextureMeshId =

    val MeshId : CompactId

    val TextureId : int

    val Type : Type

    new (meshId, textureId, typ) = { MeshId = meshId; TextureId = textureId; Type = typ }

type Shader<'Input, 'Output> = Shader of 'Input * 'Output * ShaderProgram

[<Sealed>]
type SubPipeline (context: PipelineContext, pipeline: Pipeline<unit>) =

    let releases = ResizeArray<unit -> unit> ()
    let lookup = Dictionary<Type, Dictionary<int, Texture2DBuffer * CompactManager<Mesh * obj>>> ()

    member this.Pipeline = pipeline

    member this.TryAddTextureMesh (textureBuffer: Texture2DBuffer, mesh: Mesh, extra: GpuResource) =
        let typ = extra.GetType()

        textureBuffer.TryBufferData context.GL |> ignore

        match lookup.TryGetValue (typ) with
        | true, t -> 
            let m =
                match t.TryGetValue (textureBuffer.Id) with
                | true, (_, m) -> m
                | _ ->
                    let m = CompactManager<Mesh * obj>.Create (10000)
                    t.[textureBuffer.Id] <- (textureBuffer, m)
                    m
            let meshId = m.Add (mesh, extra :> obj)
            let textureId = textureBuffer.Id

            TextureMeshId (meshId, textureId, typ)
            |> Some
        | _ ->
            None

                              
    member this.RemoveTextureMeshById (textureMeshId: TextureMeshId) = 
        let (_, m) = lookup.[textureMeshId.Type].[textureMeshId.TextureId]
        m.RemoveById (textureMeshId.MeshId)

    member this.GetLookup (typ) =
        match lookup.TryGetValue (typ) with
        | true, dict -> dict
        | _ ->
            let dict = Dictionary ()
            lookup.[typ] <- dict
            dict

and [<Sealed>] PipelineContext (gl: IGL, programCache: ProgramCache, subPipelines: (string * Pipeline<unit>) list) as this =
    
    let releases = ResizeArray<unit -> unit> ()
    let actions = ResizeArray<unit -> unit> ()

    let subPipelines = 
        let dict = Dictionary<string, SubPipeline> ()
        subPipelines
        |> List.iter (fun (key, value) -> dict.[key] <- SubPipeline (this, value))
        dict

    let subPipelineStack = Stack<SubPipeline> ()

    member val ProgramCache = programCache

    member this.CurrentSubPipeline =
        if subPipelineStack.Count = 0 then None
        else Some (subPipelineStack.Peek ())

    member this.UseSubPipeline (name) =
        match subPipelines.TryGetValue (name) with
        | true, subPipeline ->

            subPipelineStack.Push (subPipeline)

            match subPipeline.Pipeline with
            | Pipeline f -> f this

            subPipelineStack.Pop () |> ignore

        | _ -> ()

    member this.AddRelease release =
        releases.Add release

    member this.AddAction action =
        actions.Add action

    member this.TryAddMesh (subRenderer, texture, mesh, extra: GpuResource) =
        match subPipelines.TryGetValue (subRenderer) with
        | true, subPipeline ->
            subPipeline.TryAddTextureMesh (texture, mesh, extra)
        | _ -> None

    member this.Run () =
        for i = 0 to actions.Count - 1 do
            let f = actions.[i]
            f ()

    member val Time = 0.f with get, set

    member val View = Matrix4x4.Identity with get, set

    member val Projection = Matrix4x4.Identity with get, set

    member val GL = gl



and Pipeline<'a> = private Pipeline of (PipelineContext -> 'a)

type PipelineBuilder () =

    member this.Bind (Pipeline x : Pipeline<'a>, f: 'a -> Pipeline<'b>) : Pipeline<'b> = 
        Pipeline (
            fun context ->
                match f (x context) with
                | Pipeline g -> g context
        )

    member this.Bind (Pipeline x : Pipeline<List<'a>>, f: List<'a> -> Pipeline<'b>) : Pipeline<'b> = 
        Pipeline (
            fun context ->
                let result = (x context)
                match f result with
                | Pipeline g -> g context
        )

    member this.Delay (f: unit -> Pipeline<'a>) : Pipeline<'a> = 
        Pipeline (fun context -> match f () with | Pipeline x -> x context)

    member this.ReturnFrom (Pipeline x : Pipeline<'a>) : Pipeline<'a> =
        Pipeline x

    member this.Return (x: 'a) : Pipeline<'a> =
        Pipeline (fun _ -> x)

    member this.Zero () : Pipeline<unit> =
        Pipeline (fun _ -> ())

type Shader<'Input, 'Output> with

    member this.Run f =
        match this with
        | Shader (input, output, program) ->

            Pipeline (
                fun context ->
                    context.AddAction (fun () ->
                        context.GL.UseProgram program.programId
                        f input (fun () -> program.Run RenderPass.Depth)
                        program.Unbind ()
                        context.GL.UseProgram 0
                    )
                    output
            )

module Pipeline =

    let pipeline = PipelineBuilder ()

    let noOutput x = ()

    let run context p =
        match p with
        | Pipeline f -> f context

    let clear =
        Pipeline (
            fun context ->
                context.AddAction context.GL.Clear
        )

    let captureFrame width height p =
        Pipeline (
            fun context ->
                let gl = context.GL

                let renderTexture = RenderTexture (width, height)

                context.AddRelease renderTexture.Release
                
                context.AddAction (fun () ->
                    renderTexture.TryBufferData gl |> ignore
                    renderTexture.Bind gl
                )            
                
                match p with
                | Pipeline f -> f context

                context.AddAction (fun () ->
                    renderTexture.Unbind gl
                    gl.Clear ()
                )

                renderTexture
        )

    let getProgram name createInput createOutput =
        Pipeline (
            fun context ->
                let shaderProgram = context.ProgramCache.CreateShaderProgram (name)
                let input = createInput shaderProgram
                let output = createOutput shaderProgram
                let shader = Shader (input, output, shaderProgram)

                shader
        )

    let runProgram name createInput createOutput f =
        pipeline {
            let! shader = getProgram name createInput createOutput
            return! shader.Run f
        }

    let runSubPipeline name =
        Pipeline (
            fun context ->
                context.UseSubPipeline (name)
        )

    let runProgramWithMesh<'T, 'Input when 'Input :> MeshInput> name createInput init f =

        let t =
            if typeof<'T> = typeof<unit> then
                typeof<UnitResource>
            else
                typeof<'T>

        let getO =
            if typeof<'T> = typeof<unit> then
                fun (o: obj) -> Unchecked.defaultof<'T>
            else
                fun (o: obj) ->
                    if o = null then Unchecked.defaultof<'T>
                    else o :?> 'T

        Pipeline (
            fun context ->
                let program = context.ProgramCache.CreateShaderProgram (name)
                let input : 'Input = createInput program

                let draw = (fun () -> program.Run RenderPass.Depth)

                context.CurrentSubPipeline
                |> Option.iter (fun subPipeline ->

                    let lookup = subPipeline.GetLookup (t)

                    context.AddAction (fun () ->
                        context.GL.UseProgram program.programId

                        lookup
                        |> Seq.iter (fun pair ->
                            let key = pair.Key
                            let (textureBuffer, meshManager) = pair.Value

                            input.Texture.Set textureBuffer
                            input.TextureResolution.Set (Vector2 (single textureBuffer.Width, single textureBuffer.Height))

                            input.Time.Set context.Time
                            input.View.Set context.View
                            input.Projection.Set context.Projection

                            init input

                            meshManager.ForEach (fun id (mesh, o) ->
                                let o = getO o

                                input.Position.Set mesh.Position
                                input.Uv.Set mesh.Uv
                                input.Color.Set mesh.Color

                                f o input draw

                            )
                            program.Unbind ()
                        )

                        context.GL.UseProgram 0
                    )
                )
        )

    let setStencil p (value: int) =
        Pipeline (
            fun context ->
                context.AddAction (fun () ->
                    context.GL.EnableStencilTest ()
                    context.GL.DisableColorMask ()
                    context.GL.DisableDepthMask ()
                    context.GL.Stencil1 ()
                )

                run context p

                context.AddAction (fun () ->
                    context.GL.EnableDepthMask ()
                    context.GL.EnableColorMask ()
                    context.GL.DisableStencilTest ()
                )
        )

    let useStencil p (value: int) =
        Pipeline (
            fun context ->
                context.AddAction (fun () ->
                    context.GL.EnableStencilTest ()
                    context.GL.Stencil2 ()
                )

                run context p

                context.AddAction (fun () ->
                    context.GL.DisableStencilTest ()
                )
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
            let! renderTexture = captureFrame 1280 720 worldPipeline

            do! runProgram "Fullscreen" FinalInput noOutput (fun input draw ->
                input.Time.Set (getTime ())
                input.Position.Set (getPosition ())
                input.RenderTexture.Set renderTexture

                draw ()
            )
        }

// *****************************************
// *****************************************
// Renderer
// *****************************************
// *****************************************

type Renderer =
    {
        gl: IGL
        programCache: ProgramCache

        finalPipeline: PipelineContext
        finalPositionBuffer: Vector3Buffer

        mutable time: float32
    }

    static member Create (gl: IGL, fileReadAllText, subPipelines, worldPipeline) =

        let programCache = ProgramCache (gl, fileReadAllText)

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

        let finalPipelineContext = PipelineContext (gl, programCache, subPipelines)

        let renderer =
            {
                gl = gl
                programCache = programCache
                finalPipeline = finalPipelineContext
                finalPositionBuffer = positionBuffer
                time = 0.f
            }

        Final.finalPipeline worldPipeline (fun () -> renderer.time) (fun () -> renderer.finalPositionBuffer)
        |> run finalPipelineContext

        renderer

    member this.TryAddMesh (pipelineName: string, textureBuffer, mesh, extra: 'T) =
        this.finalPipeline.TryAddMesh (pipelineName, textureBuffer, mesh, extra)

    member this.Draw (time: float32) view projection =
        this.time <- time
        this.finalPipeline.Time <- time
        this.finalPipeline.Projection <- projection
        this.finalPipeline.View <- view

        this.gl.EnableDepthTest ()

        this.finalPipeline.Run ()

        this.gl.DisableDepthTest ()

// *****************************************
// *****************************************