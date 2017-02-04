namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

type Uniform<'T> =
    {
        name: string
        mutable location: int
        mutable value: 'T
        mutable isDirty: bool
    }

    member this.Set value = 
        this.value <- value
        this.isDirty <- true

type VertexAttribute<'T> =
    {
        name: string
        mutable location: int
        mutable value: 'T
    }

    member this.Set value = this.value <- value

type ShaderProgram =
    {
        programId: int
        mutable isInitialized: bool
        mutable length: int
        mutable inits: ResizeArray<unit -> unit>
        mutable binds: ResizeArray<unit -> unit>
        mutable unbinds: ResizeArray<unit -> unit>
    }

    static member Create programId =
        {
            programId = programId
            isInitialized = false
            length = 0
            inits = ResizeArray ()
            binds = ResizeArray ()
            unbinds = ResizeArray ()
        }

    member this.CreateUniform<'T> (name) =
        if this.isInitialized then failwithf "Cannot create uniform, %s. Shader already initialized." name

        let uni =
            {
                name = name
                location = -1
                value = Unchecked.defaultof<'T>
                isDirty = false
            }
            
        let initUni =
            fun () ->
                uni.location <- Backend.getUniformLocation this.programId uni.name

        let setValue =
            match uni :> obj with
            | :? Uniform<int> as uni ->              
                fun () -> 
                    if uni.isDirty && uni.location > -1 then 
                        Backend.bindUniformInt uni.location uni.value
                        uni.isDirty <- false

            | :? Uniform<Vector4> as uni ->         
                fun () -> 
                    if uni.isDirty && uni.location > -1 then 
                        Backend.bindUniformVector4 uni.location uni.value
                        uni.isDirty <- false

            | :? Uniform<Matrix4x4> as uni ->        
                fun () -> 
                    if uni.isDirty && uni.location > -1 then 
                        Backend.bindUniformMatrix4x4 uni.location uni.value
                        uni.isDirty <- false

            | :? Uniform<Texture2DBuffer> as uni ->
                fun () ->
                    if uni.isDirty && not (obj.ReferenceEquals (uni.value, null)) && uni.location > 0 then 
                        uni.value.TryBufferData () |> ignore
                        Backend.bindUniformInt uni.location 0
                        uni.value.Bind ()
                        uni.isDirty <- false

            | _ -> failwith "This should not happen."

        let bind = setValue

        let unbind =
            fun () -> uni.value <- Unchecked.defaultof<'T>

        this.inits.Add initUni
        this.inits.Add setValue

        this.binds.Add bind
        this.unbinds.Add unbind

        uni

    member this.CreateVertexAttribute<'T> (name) =
        if this.isInitialized then failwithf "Cannot create vertex attribute, %s. Shader already initialized." name

        let attrib =
            {
                name = name
                value = Unchecked.defaultof<'T>
                location = -1
            }

        let initAttrib =
            fun () ->
                attrib.location <- Backend.getAttributeLocation this.programId attrib.name

        let bufferData =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> as attrib -> fun () -> attrib.value.TryBufferData () |> ignore
            | :? VertexAttribute<Vector3Buffer> as attrib -> fun () -> attrib.value.TryBufferData () |> ignore
            | :? VertexAttribute<Vector4Buffer> as attrib -> fun () -> attrib.value.TryBufferData () |> ignore
            | _ -> failwith "Should not happen."

        let bindBuffer =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> as attrib -> fun () -> attrib.value.Bind ()
            | :? VertexAttribute<Vector3Buffer> as attrib -> fun () -> attrib.value.Bind ()
            | :? VertexAttribute<Vector4Buffer> as attrib -> fun () -> attrib.value.Bind ()
            | _ -> failwith "Should not happen."

        let size =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> -> 2
            | :? VertexAttribute<Vector3Buffer> -> 3
            | :? VertexAttribute<Vector4Buffer> -> 4
            | _ -> failwith "Should not happen."

        let getLength =
            match attrib :> obj with
            | :? VertexAttribute<Vector2Buffer> as attrib  -> fun () -> attrib.value.Length
            | :? VertexAttribute<Vector3Buffer> as attrib  -> fun () -> attrib.value.Length
            | :? VertexAttribute<Vector4Buffer> as attrib  -> fun () -> attrib.value.Length
            | _ -> failwith "Should not happen."

        let bind =
            fun () ->
                if not (obj.ReferenceEquals (attrib.value, null)) && attrib.location > -1 then
                    bufferData ()

                    this.length <-
                        let length = getLength ()

                        if this.length = 0 then
                            length
                        elif length < this.length then
                            length
                        else
                            this.length

                    bindBuffer ()

                    // TODO: this will change
                    Backend.bindVertexAttributePointer_Float attrib.location size
                    Backend.enableVertexAttribute attrib.location

        let unbind =
            fun () ->
                attrib.value <- Unchecked.defaultof<'T>

        this.inits.Add initAttrib
        this.inits.Add bufferData

        this.binds.Add bind
        this.unbinds.Add unbind

        attrib

    member this.CreateUniformInt (name) =
        this.CreateUniform<int> (name)

    member this.CreateUniformVector4 (name) =
        this.CreateUniform<Vector4> (name)

    member this.CreateUniformMatrix4x4 (name) =
        this.CreateUniform<Matrix4x4> (name)

    member this.CreateUniformTexture2D (name) =
        this.CreateUniform<Texture2DBuffer> (name)

    member this.CreateVertexAttributeVector2 (name) =
        this.CreateVertexAttribute<Vector2Buffer> (name)

    member this.CreateVertexAttributeVector3 (name) =
        this.CreateVertexAttribute<Vector3Buffer> (name)

    member this.CreateVertexAttributeVector4 (name) =
        this.CreateVertexAttribute<Vector4Buffer> (name)

    member this.Run () =

        if this.programId > 0 then

            if not this.isInitialized then

                for i = 0 to this.inits.Count - 1 do
                    let f = this.inits.[i]
                    f ()

                this.isInitialized <- true

            for i = 0 to this.binds.Count - 1 do
                let f = this.binds.[i]
                f ()

            if this.length > 0 then
                // TODO: this will change
                Backend.drawTriangles 0 this.length

            for i = 0 to this.unbinds.Count - 1 do
                let f = this.unbinds.[i]
                f ()

            this.length <- 0