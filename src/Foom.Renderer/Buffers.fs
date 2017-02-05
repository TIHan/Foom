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
            Backend.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Backend.makeVbo ()
            
            Backend.bufferVbo data (sizeof<Vector2> * data.Length) id
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
            Backend.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Backend.makeVbo ()
            
            Backend.bufferVboVector3 data (sizeof<Vector3> * data.Length) id
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
            Backend.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Backend.makeVbo ()
            
            Backend.bufferVboVector4 data (sizeof<Vector4> * data.Length) id
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
            Backend.bindVbo id

    member this.TryBufferData () =
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Backend.makeVbo ()
            
            Backend.bufferVboMatrix4x4 data (sizeof<Matrix4x4> * data.Length) id
            length <- data.Length
            queuedData <- None
            true
        | _ -> false

    member this.Id = id

type Texture2DBuffer () =

    let mutable id = 0
    let mutable width = 0
    let mutable height = 0
    let mutable queuedData = None
    let mutable isTransparent = false

    member this.Id = id

    member this.Width = width

    member this.Height = height

    member this.IsTransparent = isTransparent

    member this.Set (data: Bitmap) =
        queuedData <- Some data

    member this.Bind () =
        if id <> 0 then
            Backend.bindTexture id // this does activetexture0, change this eventually

    member this.TryBufferData () =
        match queuedData with
        | Some bmp ->
            width <- bmp.Width
            height <- bmp.Height

            isTransparent <- bmp.PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb

            let bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)

            id <- Backend.createTexture bmp.Width bmp.Height bmpData.Scan0

            bmp.UnlockBits (bmpData)
            bmp.Dispose ()
            queuedData <- None
            true
        | _ -> false

type FramebufferTexture (width, height) =

    let mutable id = 0
    let mutable isDirty = false
    let mutable width = width
    let mutable height = height

    member this.Id = id

    member this.Width = width

    member this.Height = height

    member this.Bind () =
        if id <> 0 then
            Backend.bindTexture id // this does activetexture0, change this eventually

    member this.TryBufferData () =
        if id = 0 then
            id <- Backend.createFramebufferTexture width height (nativeint 0)

type Framebuffer () =

    let mutable id = 0
    let mutable depthBufferId = 0
    let mutable queuedData = None
    let mutable width = 0
    let mutable height = 0

    member this.Set (data: FramebufferTexture) =
        queuedData <- Some data

    member this.Width = width

    member this.Height = height

    member this.Bind () =
        if id <> 0 then
           Backend.framebufferRender width height id

    member this.Render () =
        if id <> 0 then
            Backend.framebufferRender width height 0

    member this.TryBufferData () =
        
        match queuedData with
        | Some data ->
            if id = 0 then
                id <- Backend.createFramebuffer ()
                Backend.bindFramebuffer id
                depthBufferId <- Backend.createRenderbuffer data.Width data.Height
                Backend.bindFramebuffer 0
                width <- data.Width
                height <- data.Height

            Backend.bindFramebuffer id
            Backend.bindTexture data.Id
            Backend.setFramebufferTexture data.Id
            Backend.bindTexture 0
            Backend.bindFramebuffer 0
            queuedData <- None
            true
        | _ -> false

    member this.Id = id