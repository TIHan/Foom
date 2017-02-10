namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices


// *****************************************
// *****************************************
// Array Buffers
// *****************************************
// *****************************************

[<Sealed>]
type Buffer<'T when 'T : struct> (data: 'T [], bufferData: 'T [] -> int -> unit) =

    let mutable id = 0
    let mutable length = 0
    let mutable queuedData = Some data

    member this.Set (data: 'T []) =
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
            
            bufferData data id
            length <- data.Length
            queuedData <- None
            true
        | _ -> false

    member this.Id = id

    member this.Release () =
        if id <> 0 then
            Backend.deleteBuffer (id)
            id <- 0
            length <- 0
            queuedData <- None

type Vector2Buffer = Buffer<Vector2>
type Vector3Buffer = Buffer<Vector3>
type Vector4Buffer = Buffer<Vector4>

module Buffer =

    let createVector2 data =
        Vector2Buffer (data, fun data id -> Backend.bufferVbo data (sizeof<Vector2> * data.Length) id)

    let createVector3 data =
        Vector3Buffer (data, fun data id -> Backend.bufferVboVector3 data (sizeof<Vector3> * data.Length) id)

    let createVector4 data =
        Vector4Buffer (data, fun data id -> Backend.bufferVboVector4 data (sizeof<Vector4> * data.Length) id)


type Texture2DBufferQueueItem =
    | Empty
    | Data of byte [] * width: int * height: int
    | Bitmap of Bitmap

type Texture2DBuffer (data, width, height) =

    let mutable id = 0
    let mutable width = 0
    let mutable height = 0
    let mutable queuedData = Data (data, width, height)
    let mutable isTransparent = false

    member this.Id = id

    member this.Width = width

    member this.Height = height

    member this.IsTransparent = isTransparent

    member this.Set (data: Bitmap) =
        queuedData <- Bitmap data

    member this.Bind () =
        if id <> 0 then
            Backend.bindTexture id // this does activetexture0, change this eventually

    member this.TryBufferData () =
        match queuedData with
        | Data (data, w, h) ->
            width <- w
            height <- h

            if data.Length = 0 then
                id <- Backend.createTexture width height (nativeint 0)
            else
                let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
                let addr = handle.AddrOfPinnedObject ()

                id <- Backend.createTexture width height addr

                handle.Free ()

            queuedData <- Empty
            true

        | Bitmap bmp ->
            width <- bmp.Width
            height <- bmp.Height

            isTransparent <- bmp.PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb

            let bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)

            id <- Backend.createTexture bmp.Width bmp.Height bmpData.Scan0

            bmp.UnlockBits (bmpData)
            bmp.Dispose ()
            queuedData <- Empty
            true
        | _ -> false

    member this.Release () =
        if id <> 0 then
            Backend.deleteTexture (id)
            id <- 0
            width <- 0
            height <- 0
            queuedData <- Empty

type RenderTexture (width, height) =

    let mutable framebufferId = 0
    let mutable depthBufferId = 0
    let mutable stencilBufferId = 0
    let mutable textureId = 0
    let mutable width = width
    let mutable height = height

    member this.Width = width

    member this.Height = height

    member this.Bind () =
        if framebufferId <> 0 then
           Backend.bindFramebuffer framebufferId

    member this.Unbind () =
        Backend.bindFramebuffer 0

    member this.BindTexture () =
        if textureId <> 0 then
            Backend.bindTexture textureId // this does activetexture0, change this eventually    

    member this.TryBufferData () =
        
        if framebufferId = 0 then
            textureId <- Backend.createFramebufferTexture width height (nativeint 0)
            framebufferId <- Backend.createFramebuffer ()

            Backend.bindFramebuffer framebufferId
            depthBufferId <- Backend.createRenderbuffer width height
            Backend.bindFramebuffer 0

            Backend.bindFramebuffer framebufferId
            Backend.bindTexture textureId
            Backend.setFramebufferTexture textureId
            Backend.bindTexture 0
            Backend.bindFramebuffer 0
            true
        else
            false

    member this.TextureId = textureId

    member this.Release () =
        ()
        // TODO: