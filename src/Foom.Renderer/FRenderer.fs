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

module NewShader =

    type UniformValue<'T> =
        {
            mutable programId: int
            mutable value: 'T
            mutable isDirty: bool
        }

        static member (<--) (uniValue: UniformValue<'T>, value: 'T) =
            uniValue.value <- value
            uniValue.isDirty <- true

        member this.Value = this.value

    type Uniform =
        {
            name: string
            mutable location: int
        }

        static member Create (name) =
            {
                name = name
                location = 0
            }

    [<RequireQualifiedAccess>]
    type UniformKind =
        | Int of int UniformValue
        | Vector4 of Vector4 UniformValue
        | Matrix4x4 of Matrix4x4 UniformValue
        | Texture2D of Texture2DBuffer UniformValue

    type Attribute =
        {
            name: string

            mutable location: int
        }

        static member Create (name) =
            {
                name = name
                location = 0
            }

    [<RequireQualifiedAccess>]
    type AttributeKind =
        | Vector2 of Vector2Buffer ref
        | Vector3 of Vector3Buffer ref
        | Vector4 of Vector4Buffer ref

    type Shader =
        {
            mutable programId: int
            mutable isInitialized: bool
            mutable length: int
            mutable inits: ResizeArray<unit -> unit>
            mutable binds: ResizeArray<unit -> unit>
            mutable unbinds: ResizeArray<unit -> unit>
        }

        member this.AddUniform (uni: Uniform, kind: UniformKind) =

            if not this.isInitialized then

                let initSetId =
                    match kind with
                    | UniformKind.Int value ->              fun () -> value.programId <- this.programId
                    | UniformKind.Vector4 value ->          fun () -> value.programId <- this.programId
                    | UniformKind.Matrix4x4 value ->        fun () -> value.programId <- this.programId
                    | UniformKind.Texture2D value ->        fun () -> value.programId <- this.programId
            
                let initUni =
                    fun () ->
                        uni.location <- Renderer.getUniformLocation this.programId uni.name

                let setValue =
                    match kind with
                    | UniformKind.Int value ->              
                        fun () -> 
                            if value.isDirty && value.programId = this.programId && uni.location > 0 then 
                                Renderer.bindUniformInt uni.location value.value
                                value.isDirty <- false

                    | UniformKind.Vector4 value ->         
                        fun () -> 
                            if value.isDirty && value.programId = this.programId && uni.location > 0 then 
                                Renderer.bindUniformVector4 uni.location value.value
                                value.isDirty <- false

                    | UniformKind.Matrix4x4 value ->        
                        fun () -> 
                            if value.isDirty && value.programId = this.programId && uni.location > 0 then 
                                Renderer.bindUniformMatrix4x4 uni.location value.value
                                value.isDirty <- false

                    | UniformKind.Texture2D value ->
                        fun () ->
                            if value.isDirty && value.programId = this.programId && uni.location > 0 then
                                value.value.TryBufferData () |> ignore
                                Renderer.bindUniformInt uni.location value.value.Id
                                value.value.Bind ()
                                value.isDirty <- false

                let bind = setValue

                this.inits.Add initSetId
                this.inits.Add initUni
                this.inits.Add setValue

                this.binds.Add bind

        member this.AddAttribute (attrib: Attribute, kind: AttributeKind) =

            if not this.isInitialized then

                let initAttrib =
                    fun () ->
                        attrib.location <- Renderer.getAttributeLocation this.programId attrib.name

                let bufferData =
                    match kind with
                    | AttributeKind.Vector2 b -> fun () -> (!b).TryBufferData () |> ignore
                    | AttributeKind.Vector3 b -> fun () -> (!b).TryBufferData () |> ignore
                    | AttributeKind.Vector4 b -> fun () -> (!b).TryBufferData () |> ignore

                let bindBuffer =
                    match kind with
                    | AttributeKind.Vector2 b -> fun () -> (!b).Bind ()
                    | AttributeKind.Vector3 b -> fun () -> (!b).Bind ()
                    | AttributeKind.Vector4 b -> fun () -> (!b).Bind ()

                let size =
                    match kind with
                    | AttributeKind.Vector2 b -> 2
                    | AttributeKind.Vector3 b -> 3
                    | AttributeKind.Vector4 b -> 4

                let getLength =
                    match kind with
                    | AttributeKind.Vector2 b -> fun () -> (!b).Length
                    | AttributeKind.Vector3 b -> fun () -> (!b).Length
                    | AttributeKind.Vector4 b -> fun () -> (!b).Length

                let bind =
                    fun () ->
                        if attrib.location > 0 then
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
                            Renderer.bindVertexAttributePointer_Float attrib.location size

                this.inits.Add initAttrib
                this.inits.Add bufferData

                this.binds.Add bind

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
                    Renderer.drawTriangles 0 this.length

                for i = 0 to this.unbinds.Count - 1 do
                    let f = this.unbinds.[i]
                    f ()


// *****************************************
// *****************************************
// Renderer
// *****************************************
// *****************************************

type Shader =
    {
        ProgramId: int
    }

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

type Material =
    {
        Shader: Shader
        Texture: Texture
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

type FRenderer =
    {
        Lookup: Dictionary<ShaderProgramId, Shader * Dictionary<TextureId, Texture * FRendererBucket>> 
        Transparents: ResizeArray<ShaderProgramId * Material * Mesh>
    }

    static member Create () =
        {
            Lookup = Dictionary ()
            Transparents = ResizeArray ()
        }

    member this.CreateVector2Buffer (data) =
        Vector2Buffer (data)

    member this.CreateVector3Buffer (data) =
        Vector3Buffer (data)

    member this.CreateVector4Buffer (data) =
        Vector4Buffer (data)

    member this.CreateTexture2DBuffer (bmp) =
        Texture2DBuffer (bmp)

    member this.CreateShader (vertexShader, fragmentShader) =
        {
            ProgramId = Renderer.loadShaders vertexShader fragmentShader
        }

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

        material.Texture.Buffer.TryBufferData () |> ignore
        mesh.Position.TryBufferData () |> ignore
        mesh.Uv.TryBufferData () |> ignore

        // TODO: What shall we do with transparent textures? :D
//        if material.Texture.Buffer.IsTransparent then
//            None
//        else

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
                    bucketLookup.Add (texture.Buffer.Id, (texture, bucket))
                    bucket
            bucket

        let add shader =
            let bucketLookup =
                match this.Lookup.TryGetValue (shader.ProgramId) with
                | true, (_, bucketLookup) -> bucketLookup
                | _, _ ->
                    let bucketLookup = Dictionary ()
                    this.Lookup.Add (shader.ProgramId, (shader, bucketLookup))
                    bucketLookup

            material.Texture.Buffer.TryBufferData () |> ignore

            let bucket = addTexture bucketLookup material.Texture

            let idRef = bucket.Add (mesh)

            let render =
                {
                    ShaderProgramId = shader.ProgramId
                    TextureId = material.Texture.Buffer.Id
                    IdRef = idRef
                }

            Some render

        add material.Shader

    member this.ProcessPrograms f =
        this.Lookup
        |> Seq.iter (fun pair ->
            let programId = pair.Key
            let (shader, textureLookup) = pair.Value

            Renderer.useProgram programId

            textureLookup
            |> Seq.iter (fun pair ->
                let textureId = pair.Key
                let (texture, bucket) = pair.Value

                let count = bucket.Meshes.Count

                for i = 0 to count - 1 do

                    let mesh = bucket.Meshes.[i]

                    f shader texture mesh
            )
        )

    member this.Clear () =
        Renderer.clear ()

    member this.Draw (projection: Matrix4x4) (view: Matrix4x4) =

        Renderer.enableDepth ()

        let mutable drawCalls = 0

        this.ProcessPrograms (fun shader texture mesh ->
            let position = mesh.Position
            let uv = mesh.Uv
            let color = mesh.Color
            let center = mesh.Center

            let textureBuffer = texture.Buffer
            let programId = shader.ProgramId

            position.TryBufferData () |> ignore
            uv.TryBufferData () |> ignore
            color.TryBufferData () |> ignore
            center.TryBufferData () |> ignore

            let uniformProjection = Renderer.getUniformLocation programId "uni_projection"

            Renderer.setUniformMatrix4x4 uniformProjection projection

            let uniformView = Renderer.getUniformLocation programId "uni_view"

            Renderer.setUniformMatrix4x4 uniformView view

            position.Bind ()
            Renderer.bindPosition programId

            uv.Bind ()
            Renderer.bindUv programId

            color.Bind ()
            Renderer.bindColor programId

            center.Bind ()
            Renderer.bindCenter programId

            Renderer.setTexture programId textureBuffer.Id
            textureBuffer.Bind ()

            if mesh.IsWireframe then
                Renderer.drawArrays 0 position.Length
            else
                Renderer.drawTriangles 0 position.Length

            drawCalls <- drawCalls + 1
        )

        Renderer.disableDepth ()

        //printfn "Draw Calls: %A" drawCalls

// *****************************************
// *****************************************