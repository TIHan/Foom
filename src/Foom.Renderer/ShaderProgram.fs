namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.IO

[<Sealed>]
type Uniform<'T> (name) =

    member val Name = name

    member val Location = -1 with get, set

    member val Value = Unchecked.defaultof<'T> with get, set

    member val IsDirty = false with get, set

    member this.Set value =
        this.Value <- value
        this.IsDirty <- true

// TODO: Vertex attrib needs a isDirty.
[<Sealed>]
type VertexAttribute<'T> (name, divisor) =

    member val Name : string = name

    member val Location = -1 with get, set

    member val Value = Unchecked.defaultof<'T> with get, set

    member val Divisor = divisor

    member this.Set value = this.Value <- value

[<Sealed>]
type InstanceAttribute<'T> (name) =

    member val VertexAttribute = VertexAttribute<'T> (name, 1)

    member this.Set value = this.VertexAttribute.Set value

type DrawOperation =
    | Normal
    | Instanced

type RenderPass =
    | NoDepth
    | Depth
    | Stencil1
    | Stencil2

type ShaderProgram =
    {
        name: string
        programId: int
        drawOperation: DrawOperation
        mutable isUnbinded: bool
        mutable isInitialized: bool
        mutable length: int
        mutable inits: ResizeArray<unit -> unit>
        mutable binds: ResizeArray<unit -> unit>
        mutable unbinds: ResizeArray<unit -> unit>
        mutable instanceCount: int
    }

    static member Load (name) =
        let vertexBytes = File.ReadAllText (name + ".vert") |> System.Text.Encoding.UTF8.GetBytes
        let fragmentBytes = File.ReadAllText (name + ".frag") |> System.Text.Encoding.UTF8.GetBytes
        let programId = Backend.loadShaders vertexBytes fragmentBytes
        {
            name = name
            programId = programId
            drawOperation = DrawOperation.Normal
            isUnbinded = true
            isInitialized = false
            length = -1
            inits = ResizeArray ()
            binds = ResizeArray ()
            unbinds = ResizeArray ()
            instanceCount = -1
        }

    member this.CreateUniform<'T> (name) =
        if this.isInitialized then failwithf "Cannot create uniform, %s. Shader already initialized." name

        let uni = Uniform<'T> (name)
            
        let initUni =
            fun () ->
                uni.Location <- Backend.getUniformLocation this.programId uni.Name

        let setValue =
            match uni :> obj with
            | :? Uniform<int> as uni ->              
                fun () -> 
                    if uni.IsDirty && uni.Location > -1 then 
                        Backend.bindUniformInt uni.Location uni.Value
                        uni.IsDirty <- false

            | :? Uniform<float32> as uni ->              
                fun () -> 
                    if uni.IsDirty && uni.Location > -1 then 
                        Backend.bindUniform_float uni.Location uni.Value
                        uni.IsDirty <- false

            | :? Uniform<Vector2> as uni ->         
                fun () -> 
                    if uni.IsDirty && uni.Location > -1 then 
                        Backend.bindUniformVector2 uni.Location uni.Value
                        uni.IsDirty <- false

            | :? Uniform<Vector4> as uni ->         
                fun () -> 
                    if uni.IsDirty && uni.Location > -1 then 
                        Backend.bindUniformVector4 uni.Location uni.Value
                        uni.IsDirty <- false

            | :? Uniform<Matrix4x4> as uni ->        
                fun () -> 
                    if uni.IsDirty && uni.Location > -1 then 
                        Backend.bindUniformMatrix4x4 uni.Location uni.Value
                        uni.IsDirty <- false

            | :? Uniform<Texture2DBuffer> as uni ->
                fun () ->
                    if uni.IsDirty && not (obj.ReferenceEquals (uni.Value, null)) && uni.Location > -1 then 
                        uni.Value.TryBufferData () |> ignore
                        Backend.bindUniformInt uni.Location 0
                        uni.Value.Bind ()
                        uni.IsDirty <- false

            | :? Uniform<RenderTexture> as uni ->
                fun () ->
                    if uni.IsDirty && not (obj.ReferenceEquals (uni.Value, null)) && uni.Location > -1 then 
                        Backend.bindUniformInt uni.Location 0
                        uni.Value.BindTexture ()
                        uni.IsDirty <- false

            | _ -> failwith "This should not happen."

        let bind = setValue

        let unbind =
            fun () -> uni.Set Unchecked.defaultof<'T>

        this.inits.Add initUni
        this.inits.Add setValue

        this.binds.Add bind
        this.unbinds.Add unbind

        uni

    member this.CreateVertexAttribute<'T> (name) =
        if this.isInitialized then failwithf "Cannot create vertex attribute, %s. Shader already initialized." name

        let attrib = VertexAttribute<'T> (name, 0)

        this.AddVertexAttribute attrib
        attrib

    member this.CreateInstanceAttribute<'T> (name) =
        if this.isInitialized then failwithf "Cannot create instance attribute, %s. Shader already initialized." name

        let attrib = InstanceAttribute<'T> (name)

        this.AddVertexAttribute attrib.VertexAttribute
        attrib

    member this.AddVertexAttribute<'T> (attrib: VertexAttribute<'T>) =

        let initAttrib =
            fun () ->
                attrib.Location <- Backend.getAttributeLocation this.programId attrib.Name

        let bufferData =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> as attrib -> 
                fun () -> 
                    if obj.ReferenceEquals (attrib.Value, null) |> not then
                        attrib.Value.TryBufferData () |> ignore

            | :? VertexAttribute<Vector3Buffer> as attrib -> 
                fun () -> 
                    if obj.ReferenceEquals (attrib.Value, null) |> not then
                        attrib.Value.TryBufferData () |> ignore

            | :? VertexAttribute<Vector4Buffer> as attrib ->
                fun () -> 
                    if obj.ReferenceEquals (attrib.Value, null) |> not then
                        attrib.Value.TryBufferData () |> ignore

            | _ -> failwith "Should not happen."

        let bindBuffer =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> as attrib -> 
                fun () -> 
                    if obj.ReferenceEquals (attrib.Value, null) |> not then
                        attrib.Value.Bind ()

            | :? VertexAttribute<Vector3Buffer> as attrib ->
                fun () -> 
                    if obj.ReferenceEquals (attrib.Value, null) |> not then
                        attrib.Value.Bind ()

            | :? VertexAttribute<Vector4Buffer> as attrib ->
                fun () -> 
                    if obj.ReferenceEquals (attrib.Value, null) |> not then
                        attrib.Value.Bind ()

            | _ -> failwith "Should not happen."

        let size =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> -> 2
            | :? VertexAttribute<Vector3Buffer> -> 3
            | :? VertexAttribute<Vector4Buffer> -> 4
            | _ -> failwith "Should not happen."

        let getLength =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> as attrib -> fun () -> attrib.Value.Length
            | :? VertexAttribute<Vector3Buffer> as attrib -> fun () -> attrib.Value.Length
            | :? VertexAttribute<Vector4Buffer> as attrib -> fun () -> attrib.Value.Length
            | _ -> failwith "Should not happen."

        let bind =
            fun () ->
                if attrib.Location > -1 then
                    if not (obj.ReferenceEquals (attrib.Value, null)) then
                        bufferData ()
                        bindBuffer ()

                        // TODO: this will change
                        Backend.bindVertexAttributePointer_Float attrib.Location size
                        Backend.enableVertexAttribute attrib.Location
                        Backend.glVertexAttribDivisor attrib.Location attrib.Divisor

                        let length = getLength ()
                        if attrib.Divisor > 0 then
                            this.instanceCount <-
                                if this.instanceCount = -1 then
                                    length
                                elif length < this.instanceCount then
                                    length
                                else
                                    this.instanceCount
                        else
                            this.length <-
                                if this.length = -1 then
                                    length
                                elif length < this.length then
                                    length
                                else
                                    this.length
                    else
                        this.length <- 0

        let unbind =
            fun () ->
                attrib.Value <- Unchecked.defaultof<'T>

        this.inits.Add initAttrib
        this.inits.Add bufferData

        this.binds.Add bind
        this.unbinds.Add unbind

    member this.CreateUniformInt (name) =
        this.CreateUniform<int> (name)

    member this.CreateUniformFloat (name) =
        this.CreateUniform<float32> (name)

    member this.CreateUniformVector2 (name) =
        this.CreateUniform<Vector2> (name)

    member this.CreateUniformVector4 (name) =
        this.CreateUniform<Vector4> (name)

    member this.CreateUniformMatrix4x4 (name) =
        this.CreateUniform<Matrix4x4> (name)

    member this.CreateUniformTexture2D (name) =
        this.CreateUniform<Texture2DBuffer> (name)

    member this.CreateUniformRenderTexture (name) =
        this.CreateUniform<RenderTexture> (name)

    member this.CreateVertexAttributeVector2 (name) =
        this.CreateVertexAttribute<Vector2Buffer> (name)

    member this.CreateVertexAttributeVector3 (name) =
        this.CreateVertexAttribute<Vector3Buffer> (name)

    member this.CreateVertexAttributeVector4 (name) =
        this.CreateVertexAttribute<Vector4Buffer> (name)

    member this.CreateInstanceAttributeVector2 (name) =
        this.CreateInstanceAttribute<Vector2Buffer> (name)

    member this.CreateInstanceAttributeVector3 (name) =
        this.CreateInstanceAttribute<Vector3Buffer> (name)

    member this.CreateInstanceAttributeVector4 (name) =
        this.CreateInstanceAttribute<Vector4Buffer> (name)

    member this.Unbind () =
        if not this.isUnbinded then
            for i = 0 to this.unbinds.Count - 1 do
                let f = this.unbinds.[i]
                f ()

            this.length <- -1
            this.instanceCount <- -1
            this.isUnbinded <- true

    member this.Draw () =

        if this.length > 0 then
            match this.drawOperation with
            | Normal ->
                Backend.drawTriangles 0 this.length
            | Instanced ->
                if this.instanceCount > 0 then
                    Backend.drawTrianglesInstanced this.length this.instanceCount

    member this.Run (renderPass: RenderPass) =

        if this.programId > 0 then
            this.isUnbinded <- false
            if not this.isInitialized then

                for i = 0 to this.inits.Count - 1 do
                    let f = this.inits.[i]
                    f ()

                this.isInitialized <- true

            for i = 0 to this.binds.Count - 1 do
                let f = this.binds.[i]
                f ()

            match renderPass with
            | NoDepth ->
                Backend.depthMaskFalse ()
                this.Draw ()
                Backend.depthMaskTrue ()

            | Stencil1 ->
                Backend.enableStencilTest ()
                Backend.colorMaskFalse ()
                Backend.depthMaskFalse ()
                Backend.stencil1 ()
                this.Draw ()
                Backend.depthMaskTrue ()
                Backend.colorMaskTrue ()
                Backend.disableStencilTest ()

            | Stencil2 ->
                Backend.enableStencilTest ()
                Backend.stencil2 ()
                this.Draw ()
                Backend.disableStencilTest ()


            | _ -> this.Draw ()
