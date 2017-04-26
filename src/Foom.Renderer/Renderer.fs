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

type Shader (shaderProgram : ShaderProgram) =
    
    member val Position = shaderProgram.CreateVertexAttributeVector3 ("position")

    member val Uv = shaderProgram.CreateVertexAttributeVector2 ("in_uv")

    member val Color = shaderProgram.CreateVertexAttributeVector4 ("in_color")

    member val Texture = shaderProgram.CreateUniformTexture2DVarying ("uni_texture")

    member val View = shaderProgram.CreateUniformMatrix4x4 ("uni_view")

    member val Projection = shaderProgram.CreateUniformMatrix4x4 ("uni_projection")

    member val Time = shaderProgram.CreateUniformFloat ("uTime")

    member val TextureResolution = shaderProgram.CreateUniformVector2 ("uTextureResolution")

type Material (texture, shader) =

    member val OwnerCount = 0 with get, set

    member val Shader : Shader = shader

    member val Texture : Texture2DBuffer = texture

type Mesh (position, uv, color) =

    member val OwnerCount = 0 with get, set

    member val Position = Buffer.createVector3 position

    member val Uv = Buffer.createVector2 uv

    member val Color = Buffer.createVector4 color

    abstract UseShader : #Shader -> unit

    default this.UseShader (shader : #Shader) =
        shader.Position.Set this.Position
        shader.Uv.Set this.Uv
        shader.Color.Set this.Color

// *****************************************
// *****************************************
// Cache
// *****************************************
// *****************************************

type ProgramCache (gl: IGL, fileReadAllText) =
    let cache = Dictionary<string, ShaderProgram> ()

    member this.GetProgram (name : string) =
        match cache.TryGetValue (name) with
        | true, program -> program
        | _ ->
            let vertexBytes = fileReadAllText (name + ".vert")//File.ReadAllText (name + ".vert") |> System.Text.Encoding.UTF8.GetBytes
            let fragmentBytes = fileReadAllText (name + ".frag")//File.ReadAllText (name + ".frag") |> System.Text.Encoding.UTF8.GetBytes

            System.Diagnostics.Debug.WriteLine ("Loading Shader " + name)
            let programId = gl.LoadProgram (vertexBytes, fragmentBytes)//Backend.loadShaders vertexBytes fragmentBytes

            let program = ShaderProgram.Create (gl, programId)
            cache.[name] <- program

            program

    member this.Remove (name: string) =
        cache.Remove (name.ToUpper ())

// *****************************************
// *****************************************
// Shader
// *****************************************
// *****************************************

//[<Struct>]
//type TextureMeshId =

//    val MeshId : CompactId

//    val TextureId : int

//    val Type : Type

//    new (meshId, textureId, typ) = { MeshId = meshId; TextureId = textureId; Type = typ }

//type Shader<'Input, 'Output> = Shader of 'Input * 'Output * ShaderProgram

//[<Sealed>]
//type Group (context: GraphicsContext, pipeline: Pipeline<unit>) =

//    let releases = ResizeArray<unit -> unit> ()
//    let lookup = Dictionary<Type, Dictionary<int, Texture2DBuffer * CompactManager<Mesh * obj>>> ()

//    member this.Pipeline = pipeline

//    member this.TryAddTextureMesh (textureBuffer: Texture2DBuffer, mesh: Mesh, extra: GpuResource) =
//        let typ = extra.GetType()

//        textureBuffer.TryBufferData context.GL |> ignore

//        match lookup.TryGetValue (typ) with
//        | true, t -> 
//            let m =
//                match t.TryGetValue (textureBuffer.Id) with
//                | true, (_, m) -> m
//                | _ ->
//                    let m = CompactManager<Mesh * obj>.Create (10000)
//                    t.[textureBuffer.Id] <- (textureBuffer, m)
//                    m
//            let meshId = m.Add (mesh, extra :> obj)
//            let textureId = textureBuffer.Id

//            TextureMeshId (meshId, textureId, typ)
//            |> Some
//        | _ ->
//            None

                              
//    member this.RemoveTextureMeshById (textureMeshId: TextureMeshId) = 
//        let (_, m) = lookup.[textureMeshId.Type].[textureMeshId.TextureId]
//        m.RemoveById (textureMeshId.MeshId)

//    member this.GetLookup (typ) =
//        match lookup.TryGetValue (typ) with
//        | true, dict -> dict
//        | _ ->
//            let dict = Dictionary ()
//            lookup.[typ] <- dict
//            dict

//and [<Sealed>] GraphicsContext (gl: IGL, programCache: ProgramCache, groups: (int * Pipeline<unit>) list) as this =
    
//    let releases = ResizeArray<unit -> unit> ()
//    let actions = ResizeArray<unit -> unit> ()

//    let groups = 
//        let dict = Dictionary<int, Group> ()
//        groups
//        |> List.iter (fun (key, value) -> dict.[key] <- Group (this, value))
//        dict

//    let groupStack = Stack<Group> ()

//    member val ProgramCache = programCache

//    member this.CurrentGroup =
//        if groupStack.Count = 0 then None
//        else Some (groupStack.Peek ())

//    member this.DrawGroup group =
//        match groups.TryGetValue (group) with
//        | true, group ->

//            groupStack.Push group

//            match group.Pipeline with
//            | Pipeline f -> f this

//            groupStack.Pop () |> ignore

//        | _ -> ()

//    member this.AddRelease release =
//        releases.Add release

//    member this.AddAction action =
//        actions.Add action

//    member this.TryAddMesh (group, texture, mesh, extra: GpuResource) =
//        match groups.TryGetValue (group) with
//        | true, group ->
//            group.TryAddTextureMesh (texture, mesh, extra)
//        | _ -> None

//    member this.Run () =
//        for i = 0 to actions.Count - 1 do
//            let f = actions.[i]
//            f ()

//    member val Time = 0.f with get, set

//    member val View = Matrix4x4.Identity with get, set

//    member val Projection = Matrix4x4.Identity with get, set

//    member val GL = gl



//and Pipeline<'a> = private Pipeline of (GraphicsContext -> 'a)

//type PipelineBuilder () =

//    member this.Bind (Pipeline x : Pipeline<'a>, f: 'a -> Pipeline<'b>) : Pipeline<'b> = 
//        Pipeline (
//            fun context ->
//                match f (x context) with
//                | Pipeline g -> g context
//        )

//    member this.Bind (Pipeline x : Pipeline<List<'a>>, f: List<'a> -> Pipeline<'b>) : Pipeline<'b> = 
//        Pipeline (
//            fun context ->
//                let result = (x context)
//                match f result with
//                | Pipeline g -> g context
//        )

//    member this.Delay (f: unit -> Pipeline<'a>) : Pipeline<'a> = 
//        Pipeline (fun context -> match f () with | Pipeline x -> x context)

//    member this.ReturnFrom (Pipeline x : Pipeline<'a>) : Pipeline<'a> =
//        Pipeline x

//    member this.Return (x: 'a) : Pipeline<'a> =
//        Pipeline (fun _ -> x)

//    member this.Zero () : Pipeline<unit> =
//        Pipeline (fun _ -> ())

//type Shader<'Input, 'Output> with

//    member this.Run f =
//        match this with
//        | Shader (input, output, program) ->

//            Pipeline (
//                fun context ->
//                    context.AddAction (fun () ->
//                        context.GL.UseProgram program.programId
//                        f input (fun () -> program.Run RenderPass.Depth)
//                        program.Unbind ()
//                        context.GL.UseProgram 0
//                    )
//                    output
//            )

//module Pipeline =

//    let pipeline = PipelineBuilder ()

//    let noOutput x = ()

//    let run context p =
//        match p with
//        | Pipeline f -> f context

//    let clear =
//        Pipeline (
//            fun context ->
//                context.AddAction context.GL.Clear
//        )

//    let captureFrame width height p =
//        Pipeline (
//            fun context ->
//                let gl = context.GL

//                let renderTexture = RenderTexture (width, height)

//                context.AddRelease renderTexture.Release
                
//                context.AddAction (fun () ->
//                    renderTexture.TryBufferData gl |> ignore
//                    renderTexture.Bind gl
//                )            
                
//                match p with
//                | Pipeline f -> f context

//                context.AddAction (fun () ->
//                    renderTexture.Unbind gl
//                    gl.Clear ()
//                )

//                renderTexture
//        )

//    let getProgram name createInput createOutput =
//        Pipeline (
//            fun context ->
//                let shaderProgram = context.ProgramCache.CreateShaderProgram (name)
//                let input = createInput shaderProgram
//                let output = createOutput shaderProgram
//                let shader = Shader (input, output, shaderProgram)

//                shader
//        )

//    let runProgram name createInput createOutput f =
//        pipeline {
//            let! shader = getProgram name createInput createOutput
//            return! shader.Run f
//        }

//    let drawGroup group =
//        Pipeline (
//            fun context ->
//                context.DrawGroup group
//        )

//    let runProgramWithMesh<'T, 'Input when 'Input :> MeshInput> name createInput init f =

//        let t =
//            if typeof<'T> = typeof<unit> then
//                typeof<UnitResource>
//            else
//                typeof<'T>

//        let getO =
//            if typeof<'T> = typeof<unit> then
//                fun (o: obj) -> Unchecked.defaultof<'T>
//            else
//                fun (o: obj) ->
//                    if o = null then Unchecked.defaultof<'T>
//                    else o :?> 'T

//        Pipeline (
//            fun context ->
//                let program = context.ProgramCache.CreateShaderProgram (name)
//                let input : 'Input = createInput program

//                let draw = (fun () -> program.Run RenderPass.Depth)

//                context.CurrentGroup
//                |> Option.iter (fun group ->

//                    let lookup = group.GetLookup (t)

//                    context.AddAction (fun () ->
//                        context.GL.UseProgram program.programId

//                        lookup
//                        |> Seq.iter (fun pair ->
//                            let key = pair.Key
//                            let (textureBuffer, meshManager) = pair.Value

//                            // TODO: generates garbage
//                            input.Texture.Set [|textureBuffer|]
//                            input.TextureResolution.Set (Vector2 (single textureBuffer.Width, single textureBuffer.Height))

//                            input.Time.Set context.Time
//                            input.View.Set context.View
//                            input.Projection.Set context.Projection

//                            init input

//                            meshManager.ForEach (fun id (mesh, o) ->
//                                let o = getO o

//                                input.Position.Set mesh.Position
//                                input.Uv.Set mesh.Uv
//                                input.Color.Set mesh.Color

//                                f o input draw

//                            )
//                            program.Unbind ()
//                        )

//                        context.GL.UseProgram 0
//                    )
//                )
//        )

//    let setStencil p (value: int) =
//        Pipeline (
//            fun context ->
//                context.AddAction (fun () ->
//                    context.GL.EnableStencilTest ()
//                    context.GL.DisableColorMask ()
//                    context.GL.DisableDepthMask ()
//                    context.GL.Stencil1 ()
//                )

//                run context p

//                context.AddAction (fun () ->
//                    context.GL.EnableDepthMask ()
//                    context.GL.EnableColorMask ()
//                    context.GL.DisableStencilTest ()
//                )
//        )

//    let useStencil p (value: int) =
//        Pipeline (
//            fun context ->
//                context.AddAction (fun () ->
//                    context.GL.EnableStencilTest ()
//                    context.GL.Stencil2 ()
//                )

//                run context p

//                context.AddAction (fun () ->
//                    context.GL.DisableStencilTest ()
//                )
//        )

//open Pipeline

// *****************************************
// *****************************************
// Final Output Program
// *****************************************
// *****************************************

//module Final =

//    [<Sealed>]
//    type FinalInput (shaderProgram: ShaderProgram) =

//        member val Time = shaderProgram.CreateUniformFloat ("time")

//        member val RenderTexture = shaderProgram.CreateUniformRenderTexture ("uni_texture")

//        member val Position = shaderProgram.CreateVertexAttributeVector3 ("position")

//    let pipeline worldPipeline (getTime: unit -> float32) (getPosition: unit -> Vector3Buffer) =
//        pipeline {
//            let! renderTexture = captureFrame 1280 720 worldPipeline

//            do! runProgram "Fullscreen" FinalInput noOutput (fun input draw ->
//                input.Time.Set (getTime ())
//                input.Position.Set (getPosition ())
//                input.RenderTexture.Set renderTexture

//                draw ()
//            )
//        }

[<Sealed>]
type RenderCamera () =

    member val Projection = Matrix4x4.Identity with get, set

    member val View = Matrix4x4.Identity with get, set

    member val Layer = 0 with get, set

type RenderLayer (gl : IGL, programCache : ProgramCache) =

    let lookup = Dictionary<int, Shader * Dictionary<int, Texture2DBuffer * UnsafeResizeArray<Mesh>>> ()

    member this.AddMesh (material : Material, mesh : Mesh) =
        programCache.InitShaderProgram material.Shader.Program

        material.Texture.TryBufferData gl |> ignore

        let meshes =
            let lookup =
                match lookup.TryGetValue (material.Shader.Program.Id) with
                | true, (_, lookup) -> lookup
                | _ ->
                    let texLookup = Dictionary ()
                    lookup.[material.Shader.Program.Id] <- (material.Shader, texLookup)
                    texLookup

            match lookup.TryGetValue (material.Texture.Id) with
            | true, (_, meshes) -> meshes
            | _ ->
                let meshes = UnsafeResizeArray<Mesh * GpuResource>.Create 0
                lookup.[material.Texture.Id] <- (material.Texture, meshes)
                meshes

               
        meshes.Add (mesh, extra)

        mesh.OwnerCount <- mesh.OwnerCount + 1

    member this.Draw time (camera : RenderCamera) =
        lookup
        |> Seq.iter (fun pair ->
            let (shader, lookup) = pair.Value

            gl.UseProgram shader.Program.Id

            shader.Time.Set time
            shader.View.Set camera.View
            shader.Projection.Set camera.Projection

            lookup
            |> Seq.iter (fun pair ->

                let (texBuf, meshes) = pair.Value

                shader.Texture.Set [|texBuf|]
                shader.TextureResolution.Set (Vector2 (single texBuf.Width, single texBuf.Height))

                meshes.Buffer
                |> Array.iter (fun (mesh, extra) ->
                )


//                        context.GL.UseProgram program.programId

//                        lookup
//                        |> Seq.iter (fun pair ->
//                            let key = pair.Key
//                            let (textureBuffer, meshManager) = pair.Value

//                            // TODO: generates garbage
//                            input.Texture.Set [|textureBuffer|]
//                            input.TextureResolution.Set (Vector2 (single textureBuffer.Width, single textureBuffer.Height))

//                            input.Time.Set context.Time
//                            input.View.Set context.View
//                            input.Projection.Set context.Projection

//                            init input

//                            meshManager.ForEach (fun id (mesh, o) ->
//                                let o = getO o

//                                input.Position.Set mesh.Position
//                                input.Uv.Set mesh.Uv
//                                input.Color.Set mesh.Color

//                                f o input draw

//                            )
//                            program.Unbind ()
//                        )

//                        context.GL.UseProgram 0
            )
        )

// *****************************************
// *****************************************
// Renderer
// *****************************************
// *****************************************

type Renderer =
    {
        gl: IGL
        programCache: ProgramCache

        mutable time: float32
    }

    static member Create (gl: IGL, fileReadAllText) =

        let programCache = ProgramCache (gl, fileReadAllText)

        //let vertices =
        //    [|
        //        Vector3 (-1.f,-1.f, 0.f)
        //        Vector3 (1.f, -1.f, 0.f)
        //        Vector3 (1.f, 1.f, 0.f)
        //        Vector3 (1.f, 1.f, 0.f)
        //        Vector3 (-1.f,  1.f, 0.f)
        //        Vector3 (-1.f, -1.f, 0.f)
        //    |]

        //let positionBuffer = Buffer.createVector3 vertices

        let renderer =
            {
                gl = gl
                programCache = programCache
                time = 0.f
            }

        renderer

    member this.AddMesh (layer : int, material : Material, mesh : Mesh, extra: GpuResource) =
        ()

    member this.Draw (time: float32) view projection =
        this.time <- time

        this.gl.EnableDepthTest ()

        this.gl.DisableDepthTest ()

// *****************************************
// *****************************************