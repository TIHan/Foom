namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

// *****************************************
// *****************************************
// Array Buffers
// *****************************************
// *****************************************

type Vector2Buffer (data) =

    let mutable id = 0
    let mutable length = 0
    let mutable queuedData = Some data

    member this.Set (data: Vector2 []) =
        queuedData <- Some data

    member this.Length = length

    member this.Bind () =
        if id <> 0 then
            Renderer.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Renderer.makeVbo ()
            
            Renderer.bufferVbo data (sizeof<Vector2> * data.Length) id
            length <- data.Length
            queuedData <- None
            true
        | _ -> false

    member this.Id = id

type Vector3Buffer (data) =

    let mutable id = 0
    let mutable length = 0
    let mutable queuedData = Some data

    member this.Set (data: Vector3 []) =
        queuedData <- Some data

    member this.Length = length

    member this.Bind () =
        if id <> 0 then
            Renderer.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Renderer.makeVbo ()
            
            Renderer.bufferVboVector3 data (sizeof<Vector3> * data.Length) id
            length <- data.Length
            queuedData <- None
            true
        | _ -> false

    member this.Id = id

type Vector4Buffer (data) =

    let mutable id = 0
    let mutable length = 0
    let mutable queuedData = Some data

    member this.Set (data: Vector4 []) =
        queuedData <- Some data

    member this.Length = length

    member this.Bind () =
        if id <> 0 then
            Renderer.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Renderer.makeVbo ()
            
            Renderer.bufferVboVector4 data (sizeof<Vector4> * data.Length) id
            length <- data.Length
            queuedData <- None
            true
        | _ -> false

    member this.Id = id

type Matrix4x4Buffer (data) =

    let mutable id = 0
    let mutable length = 0
    let mutable queuedData = Some data

    member this.Set (data: Matrix4x4 []) =
        queuedData <- Some data

    member this.Length = length

    member this.Bind () =
        if id <> 0 then
            Renderer.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Renderer.makeVbo ()
            
            Renderer.bufferVboMatrix4x4 data (sizeof<Matrix4x4> * data.Length) id
            length <- data.Length
            queuedData <- None
            true
        | _ -> false

    member this.Id = id

type Texture2DBuffer (bmp: Bitmap) =

    let mutable id = 0
    let mutable width = bmp.Width
    let mutable height = bmp.Height
    let mutable queuedData = Some bmp
    let mutable isTransparent = false

    member this.Id = id

    member this.Width = width

    member this.Height = height

    member this.IsTransparent = isTransparent

    member this.Bind () =
        if id <> 0 then
            Renderer.bindTexture id // this does activetexture0, change this eventually

    member this.TryBufferData () =
        match queuedData with
        | Some bmp ->
            isTransparent <- bmp.PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb

            let bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)

            id <- Renderer.createTexture bmp.Width bmp.Height bmpData.Scan0

            bmp.UnlockBits (bmpData)
            bmp.Dispose ()
            queuedData <- None
            true
        | _ -> false

// *****************************************
// *****************************************

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
                uni.location <- Renderer.getUniformLocation this.programId uni.name

        let setValue =
            match uni :> obj with
            | :? Uniform<int> as uni ->              
                fun () -> 
                    if uni.isDirty && uni.location > -1 then 
                        Renderer.bindUniformInt uni.location uni.value
                        uni.isDirty <- false

            | :? Uniform<Vector4> as uni ->         
                fun () -> 
                    if uni.isDirty && uni.location > -1 then 
                        Renderer.bindUniformVector4 uni.location uni.value
                        uni.isDirty <- false

            | :? Uniform<Matrix4x4> as uni ->        
                fun () -> 
                    if uni.isDirty && uni.location > -1 then 
                        Renderer.bindUniformMatrix4x4 uni.location uni.value
                        uni.isDirty <- false

            | :? Uniform<Texture2DBuffer> as uni ->
                fun () ->
                    if uni.isDirty && not (obj.ReferenceEquals (uni.value, null)) && uni.location > 0 then 
                        uni.value.TryBufferData () |> ignore
                        Renderer.bindUniformInt uni.location 0
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
                attrib.location <- Renderer.getAttributeLocation this.programId attrib.name

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
                    Renderer.bindVertexAttributePointer_Float attrib.location size
                    Renderer.enableVertexAttribute attrib.location

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
                Renderer.drawTriangles 0 this.length

            for i = 0 to this.unbinds.Count - 1 do
                let f = this.unbinds.[i]
                f ()

            this.length <- 0


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
        Center: Vector3Buffer
        IsWireframe: bool
    }

[<ReferenceEquality>]
type FRendererBucket =
    {
        Meshes: Mesh ResizeArray
        IdRefs: int Ref ResizeArray
    }

    member this.Add (mesh: Mesh) =
        let idRef = ref this.IdRefs.Count

        this.Meshes.Add (mesh)
        this.IdRefs.Add (idRef)

        idRef

    member this.RemoveById id =
        let lastIndex = this.IdRefs.Count - 1

        this.Meshes.[id] <- this.Meshes.[lastIndex]
        this.IdRefs.[id] <- this.IdRefs.[lastIndex]
        this.IdRefs.[id] := id

        this.Meshes.RemoveAt (lastIndex)
        this.IdRefs.RemoveAt (lastIndex)

type ShaderProgramId = int
type TextureId = int

type MeshRender =
    {
        ShaderProgramId: int
        TextureId: int
        IdRef: int ref
    }

type MeshInput =
    {
        Position:   VertexAttribute<Vector3Buffer>
        Uv:         VertexAttribute<Vector2Buffer>
        Texture:    Uniform<Texture2DBuffer>
        View:       Uniform<Matrix4x4>
        Projection: Uniform<Matrix4x4>
    }

type ShaderType =
    | Normal
    | Texture
    | TextureMesh

type Shader =
    {
        ShaderProgram: ShaderProgram
    }

type Material =
    {
        Shader: Shader
        Texture: Texture
    }

type FRenderer =
    {
        Shaders: Dictionary<ShaderProgramId, Matrix4x4 -> Matrix4x4 -> unit>
        Lookup: Dictionary<ShaderProgramId, Dictionary<TextureId, Texture * FRendererBucket>> 
    }

    static member Create () =
        {
            Shaders = Dictionary ()
            Lookup = Dictionary ()
        }

    member this.CreateVector2Buffer (data) =
        Vector2Buffer (data)

    member this.CreateVector3Buffer (data) =
        Vector3Buffer (data)

    member this.CreateVector4Buffer (data) =
        Vector4Buffer (data)

    member this.CreateTexture2DBuffer (bmp) =
        Texture2DBuffer (bmp)

    member this.CreateShader (vertexShader, fragmentShader, f: ShaderProgram -> (Matrix4x4 -> Matrix4x4 -> unit)) =
        let shaderProgram =
            Renderer.loadShaders vertexShader fragmentShader
            |> ShaderProgram.Create

        this.Shaders.[shaderProgram.programId] <- f shaderProgram

        {
            ShaderProgram = shaderProgram
        }

    // TextureMesh shader
    member this.CreateShader (vertexShader, fragmentShader) =
        this.CreateShader (vertexShader, fragmentShader,

            fun shaderProgram ->
                let in_position = shaderProgram.CreateVertexAttributeVector3 ("position")
                let in_uv = shaderProgram.CreateVertexAttributeVector2 ("in_uv")
                let in_texture = shaderProgram.CreateUniformTexture2D ("uni_texture")
                let uni_view = shaderProgram.CreateUniformMatrix4x4 ("uni_view")
                let uni_projection = shaderProgram.CreateUniformMatrix4x4 ("uni_projection")

                fun view projection ->
                    this.Lookup
                    |> Seq.iter (fun pair ->
                        let programId = pair.Key
                        let textureLookup = pair.Value

                        Renderer.useProgram programId

                        textureLookup
                        |> Seq.iter (fun pair ->
                            let textureId = pair.Key
                            let (texture, bucket) = pair.Value

                            let count = bucket.Meshes.Count

                            for i = 0 to count - 1 do

                                let mesh = bucket.Meshes.[i]

                                in_position.Set     mesh.Position
                                in_uv.Set           mesh.Uv
                                in_texture.Set      texture.Buffer
                                uni_view.Set        view
                                uni_projection.Set  projection

                                let color = mesh.Color
                                let center = mesh.Center

                                let programId = shaderProgram.programId

                                color.TryBufferData () |> ignore
                                center.TryBufferData () |> ignore

                                color.Bind ()
                                Renderer.bindColor programId

                                center.Bind ()
                                Renderer.bindCenter programId

                                shaderProgram.Run ()
                        )
                    ) 

        )

    member this.CreateTexture (bmp) =
        {
            Buffer = Texture2DBuffer (bmp)
        }

    member this.CreateMaterial (shader, texture) =
        {
            Shader = shader
            Texture = texture
        }

    member this.CreateMesh (position, uv, color: Color [], center, isWireframe) =
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
            Center = Vector3Buffer (center)
            IsWireframe = isWireframe
        }

    member this.TryAdd (material: Material, mesh: Mesh) =

        let addTexture (bucketLookup: Dictionary<TextureId, Texture * FRendererBucket>) texture = 
            let bucket =
                match bucketLookup.TryGetValue (texture.Buffer.Id) with
                | true, (_, bucket) -> bucket
                | _ ->
                    let bucket =
                        {
                            Meshes = ResizeArray ()
                            IdRefs = ResizeArray ()
                        }

                    // Need to do this to make sure we get an id.
                    texture.Buffer.TryBufferData () |> ignore

                    bucketLookup.Add (texture.Buffer.Id, (texture, bucket))
                    bucket
            bucket

        let add shader =
            let shaderProgram = shader.ShaderProgram
            let bucketLookup =
                match this.Lookup.TryGetValue (shaderProgram.programId) with
                | true, (bucketLookup) -> bucketLookup
                | _, _ ->
                    let bucketLookup = Dictionary ()
                    this.Lookup.Add (shader.ShaderProgram.programId, bucketLookup)
                    bucketLookup

            let bucket = addTexture bucketLookup material.Texture

            let idRef = bucket.Add (mesh)

            let render =
                {
                    ShaderProgramId = shaderProgram.programId
                    TextureId = material.Texture.Buffer.Id
                    IdRef = idRef
                }

            Some render

        add material.Shader

    member this.Clear () =
        Renderer.clear ()

    member this.Draw (projection: Matrix4x4) (view: Matrix4x4) =
        
        Renderer.enableDepth ()

        this.Shaders
        |> Seq.iter (fun pair ->
            pair.Value view projection
        )

        Renderer.disableDepth ()

// *****************************************
// *****************************************