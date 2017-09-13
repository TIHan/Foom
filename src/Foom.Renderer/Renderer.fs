namespace Foom.Renderer

open System
open System.Numerics
open System.Collections.Generic
open System.IO

open Foom.Collections

[<Sealed>]
type internal ShaderVal<'T> (subscribe) =

    member val Subscribe : ('T -> unit) -> IDisposable = subscribe with get, set 

[<Sealed>]
type ShaderVar<'T> (initialValue) as this =

    let callbacks = ResizeArray<'T -> unit> ()

    member val internal Value = initialValue with get, set

    member this.Set value =
        this.Value <- value
        this.Notify ()

    member internal this.Update f =
        this.Value <- f this.Value
        this.Notify ()

    member internal this.Notify () =
        callbacks
        |> Seq.toList
        |> Seq.iter (fun f -> f this.Value)

    member val internal Val =
        ShaderVal<'T> (fun callback ->
            callbacks.Add callback
            callback this.Value
            {
                new IDisposable with
                    member this.Dispose () =
                        callbacks.Remove callback |> ignore
            }
        )

[<Sealed>]
type ShaderInput () =

    let programVars = ResizeArray ()

    member val ProgramVars = programVars

    member this.CreateVar<'T> (defaultValue) = ShaderVar (defaultValue)

    member this.CreateUniformVar<'T> name =
        let var = ShaderVar<'T> (Unchecked.defaultof<'T>)

        let f : ShaderProgram -> IDisposable =
            fun program ->
                let input = program.CreateUniform<'T> name
                let subscription = var.Val.Subscribe input.Set

                input.Set Unchecked.defaultof<'T>
                input.IsDirty <- false

                subscription

        programVars.Add (f)

        var

    member this.CreateVertexAttributeVar<'T> name =
        let var = ShaderVar<'T> (Unchecked.defaultof<'T>)

        let f : ShaderProgram -> IDisposable =
            fun program ->
                let input = program.CreateVertexAttribute<'T> name
                let subscription = var.Val.Subscribe input.Set

                input.Set Unchecked.defaultof<'T>

                subscription

        programVars.Add (f)

        var

    member this.CreateInstanceAttributeVar<'T> name =
        let var = ShaderVar<'T> (Unchecked.defaultof<'T>)

        let f : ShaderProgram -> IDisposable =
            fun program ->
                let input = program.CreateInstanceAttribute<'T> name
                let subscription = var.Val.Subscribe input.Set

                input.Set Unchecked.defaultof<'T>

                subscription

        programVars.Add (f)

        var

// *****************************************
// *****************************************
// Mesh
// *****************************************
// *****************************************

type MeshInput (shaderInput : ShaderInput) =

    member val Position = shaderInput.CreateVertexAttributeVar<Vector3Buffer> ("position")

    member val Uv = shaderInput.CreateVertexAttributeVar<Vector2Buffer> ("in_uv")

    member val Color = shaderInput.CreateVertexAttributeVar<Vector4Buffer> ("in_color")

    member val Texture = shaderInput.CreateUniformVar<Texture2DBuffer []> ("uni_texture")

    member val View = shaderInput.CreateUniformVar<Matrix4x4> ("uni_view")

    member val Projection = shaderInput.CreateUniformVar<Matrix4x4> ("uni_projection")

    member val Time = shaderInput.CreateUniformVar<float32> ("uTime")

    member val TextureResolution = shaderInput.CreateUniformVar<Vector2> ("uTextureResolution")

type ShaderPassProperty =
    | Stencil1
    | Stencil2
    | Stencil of value : ShaderVar<int>

[<AbstractClass>]
type BaseShaderPass (shaderProgramName : string) =

    member val ShaderProgramName = shaderProgramName

    member val CreateProperties : MeshInput -> ShaderPassProperty list = fun _ -> [] with get, set

    member val Program = None with get, set

    member val Properties : ShaderPassProperty list = [] with get, set

type ShaderPass<'T when 'T :> MeshInput> (createProperties : 'T -> ShaderPassProperty list, shaderProgramName) as this =
    inherit BaseShaderPass (shaderProgramName)

    do
        this.CreateProperties <- fun input -> createProperties (input :?> 'T)

type BaseShader (order : int, pass : BaseShaderPass, createInput : ShaderInput -> MeshInput) =

    member val internal Id = -1 with get, set

    member val Order = order

    member val Pass = pass

    member val ShaderInput = ShaderInput ()

    member val CreateInput = createInput

    member val Input : MeshInput option = None with get, set

[<Sealed>]
type Shader<'T when 'T :> MeshInput> (order, pass, createInput : ShaderInput -> 'T) =
    inherit BaseShader (order, pass, fun shaderInput -> createInput shaderInput :> MeshInput)

[<AbstractClass>]
type BaseMesh () =

    member val OwnerCount = 0 with get, set

    abstract SetBaseShaderInput : MeshInput -> unit

[<AbstractClass>]
type Mesh<'T when 'T :> MeshInput> (position, uv, color) =
    inherit BaseMesh ()

    member val Position = Buffer.createVector3 position

    member val Uv = Buffer.createVector2 uv

    member val Color = Buffer.createVector4 color

    abstract SetShaderInput : 'T -> unit

    default this.SetShaderInput (input : 'T) =
        input.Position.Set this.Position
        input.Uv.Set this.Uv
        input.Color.Set this.Color

    override this.SetBaseShaderInput input =
        match input with
        | :? 'T as input -> this.SetShaderInput input
        | _ -> failwith "shouldn't happen"

[<Sealed>]
type Mesh (position, uv, color) =
    inherit Mesh<MeshInput> (position, uv, color)

// *****************************************
// *****************************************
// Cache
// *****************************************
// *****************************************

type ProgramCache (gl: IGL, fileReadAllText) =
    let cache = Dictionary<string, int> ()

    member this.GetProgram (name : string) =
        let programId =
            match cache.TryGetValue (name) with
            | true, program -> program
            | _ ->
                let vertexBytes = fileReadAllText (name + ".vert")//File.ReadAllText (name + ".vert") |> System.Text.Encoding.UTF8.GetBytes
                let fragmentBytes = fileReadAllText (name + ".frag")//File.ReadAllText (name + ".frag") |> System.Text.Encoding.UTF8.GetBytes

                System.Diagnostics.Debug.WriteLine ("Loading Shader " + name)
                let programId = gl.LoadProgram (vertexBytes, fragmentBytes)//Backend.loadShaders vertexBytes fragmentBytes

                cache.[name] <- programId

                programId

        ShaderProgram.Create (gl, programId)

    member this.Remove (name: string) =
        cache.Remove (name.ToUpper ())

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
type RenderCamera (view, projection) =

    member val internal IdRef = ref -1 with get, set

    member val Projection = projection with get, set

    member val View = view with get, set

    member val Layer = 0 with get, set

type RenderLayer (gl : IGL) =

    let lookup = Dictionary<int, BaseShader * Dictionary<int, Texture2DBuffer * UnsafeResizeArray<BaseMesh>>> ()

    member this.AddMesh (shader : BaseShader, texBuf : Texture2DBuffer, mesh : BaseMesh) =
        let program, input =
            match shader.Pass.Program, shader.Input with
            | Some program, Some input -> program, input
            | _ -> failwith "this shouldn't happen"

        texBuf.TryBufferData gl |> ignore

        let meshes =
            let lookup =
                match lookup.TryGetValue (shader.Id) with
                | true, (_, lookup) -> lookup
                | _ ->
                    let texLookup = Dictionary ()
                    lookup.[shader.Id] <- (shader, texLookup)
                    texLookup

            match lookup.TryGetValue (texBuf.Id) with
            | true, (_, meshes) -> meshes
            | _ ->
                let meshes = UnsafeResizeArray<BaseMesh>.Create 1
                lookup.[texBuf.Id] <- (texBuf, meshes)
                meshes
               
        meshes.Add (mesh)

        mesh.OwnerCount <- mesh.OwnerCount + 1

    member this.Draw time (camera : RenderCamera) =
        lookup
        |> Seq.sortBy (fun pair ->
            let (shader, _) = pair.Value
            shader.Order
        )
        |> Seq.iter (fun pair ->
            let (shader, lookup) = pair.Value

            let program : ShaderProgram = shader.Pass.Program.Value
            let input = shader.Input.Value

            shader.Pass.Properties
            |> List.iter (function
                | Stencil1 ->
                    gl.EnableStencilTest ()
                    gl.DisableColorMask ()
                    gl.DisableDepthMask ()
                    gl.Stencil1 ()
                | Stencil2 ->
                    gl.EnableStencilTest ()
                    gl.Stencil2 ()
                | _ -> ()
            )

            gl.UseProgram program.Id

            lookup
            |> Seq.iter (fun pair ->

                input.Time.Set time
                input.View.Set camera.View
                input.Projection.Set camera.Projection

                let (texBuf, meshes) = pair.Value

                input.Texture.Set [|texBuf|]
                input.TextureResolution.Set (Vector2 (single texBuf.Width, single texBuf.Height))

                meshes.Buffer
                |> Array.iter (fun (mesh) ->
                    mesh.SetBaseShaderInput input

                    program.Run ()
                )

                program.Unbind ()
            )

            gl.UseProgram 0

            shader.Pass.Properties
            |> List.iter (function
                | Stencil1 ->
                    gl.EnableDepthMask ()
                    gl.EnableColorMask ()
                    gl.DisableStencilTest ()
                | Stencil2 ->
                    gl.DisableStencilTest ()
                | _ -> ()
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
        layers : RenderLayer []
        cameras : CompactManager<RenderCamera>
        mutable nextShaderId : int
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
                layers = Array.init 32 (fun _ -> RenderLayer (gl))
                cameras = CompactManager<RenderCamera> (1)
                nextShaderId = 0
            }

        renderer

    member this.AddMesh (layer : int, shader : Shader<'T>, texBuf : Texture2DBuffer, mesh : Mesh<'T>) =
        match shader.Pass.Program, shader.Input with
        | None, None ->
            let program = this.programCache.GetProgram shader.Pass.ShaderProgramName
            let input = shader.CreateInput shader.ShaderInput

            shader.ShaderInput.ProgramVars
            |> Seq.iter (fun f -> f program |> ignore)

            shader.Pass.Properties <- shader.Pass.CreateProperties input
            shader.Pass.Program <- Some program
            shader.Input <- Some input
            shader.Id <- this.nextShaderId
            this.nextShaderId <- this.nextShaderId + 1
        | Some program, Some input -> ()
        | _ -> failwith "this shouldn't happen"

        let layer = this.layers.[layer]
        layer.AddMesh (shader, texBuf, mesh)

    member this.CreateCamera (view, projection) =
        let camera = RenderCamera (view, projection)
        let id = this.cameras.Add (camera)
        camera.IdRef <- id.Index
        camera

    member this.Draw (time: float32) =
        this.gl.Clear ()

        this.gl.EnableDepthTest ()

        this.cameras.ForEach (fun id camera ->

            for i = 0 to this.layers.Length - 1 do
                let layer = this.layers.[i]
                layer.Draw time camera

        )

        this.gl.DisableDepthTest ()

// *****************************************
// *****************************************