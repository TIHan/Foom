namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices

type DesktopGL () =

    interface IGL with

        member this.BindBuffer id =
            Backend.bindVbo id

        member this.CreateBuffer () =
            Backend.makeVbo ()

        member this.DeleteBuffer id =
            Backend.deleteBuffer id

        member this.BufferData (data, count, id) =
            Backend.bufferVbo data count id

        member this.BufferData (data, count, id) =
            Backend.bufferVboVector3 data count id

        member this.BufferData (data, count, id) =
            Backend.bufferVboVector4 data count id

        member this.BindTexture id =
            Backend.bindTexture id

        member this.CreateTexture (width, height, data) =
            Backend.createTexture width height data

        member this.DeleteTexture id =
            Backend.deleteTexture id

        member this.BindFramebuffer id =
            Backend.bindFramebuffer id

        member this.CreateFramebuffer () =
            Backend.createFramebuffer ()

        member this.CreateFramebufferTexture (width, height, data) =
            Backend.createFramebufferTexture width height data

        member this.SetFramebufferTexture id =
            Backend.setFramebufferTexture id

        member this.CreateRenderBuffer (width, height) =
            Backend.createRenderbuffer width height

        member this.Clear () =
            Backend.clear ()

// *****************************************
// *****************************************
// Array Buffers
// *****************************************
// *****************************************

[<Sealed>]
type Buffer<'T when 'T : struct> (data: 'T [], bufferData: IGL -> 'T [] -> int -> int -> unit) =

    let mutable id = 0
    let mutable length = 0
    let mutable queuedData = Some (data, data.Length)

    member this.Set (data: 'T []) =
        queuedData <- Some (data, data.Length)

    member this.Set (data: 'T [], size) =
        queuedData <- Some (data, size)

    member this.Length = length

    member this.Bind (gl: IGL) =
        if id <> 0 then
            gl.BindBuffer id

    member this.TryBufferData (gl: IGL) =
        match queuedData with
        | Some (data, size) ->
            if id = 0 then
                id <- gl.CreateBuffer ()
            
            bufferData gl data size id
            length <- size
            queuedData <- None
            true
        | _ -> false

    member this.Id = id

    member this.Release (gl: IGL) =
        if id <> 0 then
            gl.DeleteBuffer id
            id <- 0
            length <- 0
            queuedData <- None

type Vector2Buffer = Buffer<Vector2>
type Vector3Buffer = Buffer<Vector3>
type Vector4Buffer = Buffer<Vector4>

module Buffer =

    let createVector2 data =
        Vector2Buffer (data, fun gl data size id -> gl.BufferData (data, (sizeof<Vector2> * size), id))

    let createVector3 data =
        Vector3Buffer (data, fun gl data size id -> gl.BufferData (data, (sizeof<Vector3> * size), id))

    let createVector4 data =
        Vector4Buffer (data, fun gl data size id -> gl.BufferData (data, (sizeof<Vector4> * size), id))


type Texture2DBufferQueueItem =
    | Empty
    | Data of byte []
    | Bitmap of Bitmap

type Texture2DBuffer (data, width, height) =

    let mutable id = 0
    let mutable width = width
    let mutable height = height
    let mutable queuedData = Data (data)
    let mutable isTransparent = false

    member this.Id = id

    member this.Width = width

    member this.Height = height

    member this.IsTransparent = isTransparent

    member this.Set (bmp: Bitmap) =
        width <- bmp.Width
        height <- bmp.Height
        queuedData <- Bitmap bmp

    member this.Bind (gl: IGL) =
        if id <> 0 then
            gl.BindTexture id // this does activetexture0, change this eventually  

    member this.TryBufferData (gl: IGL) =
        match queuedData with
        | Data (data) ->
            if data.Length = 0 then
                id <- gl.CreateTexture (width, height, nativeint 0)
            else
                let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
                let addr = handle.AddrOfPinnedObject ()

                id <- gl.CreateTexture (width, height, addr)

                handle.Free ()

            queuedData <- Empty
            true

        | Bitmap bmp ->
            isTransparent <- bmp.PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb

            let bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)

            id <- gl.CreateTexture (bmp.Width, bmp.Height, bmpData.Scan0)

            bmp.UnlockBits (bmpData)
            bmp.Dispose ()
            queuedData <- Empty
            true
        | _ -> false

    member this.Release (gl: IGL) =
        if id <> 0 then
            gl.DeleteTexture id
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

    member this.Bind (gl: IGL) =
        if framebufferId <> 0 then
            gl.BindFramebuffer framebufferId

    member this.Unbind () =
        Backend.bindFramebuffer 0

    member this.BindTexture () =
        if textureId <> 0 then
            Backend.bindTexture textureId

    member this.TryBufferData (gl: IGL) =
        
        if framebufferId = 0 then
            textureId <- gl.CreateFramebufferTexture (width, height, nativeint 0)//Backend.createFramebufferTexture width height (nativeint 0)
            framebufferId <- gl.CreateFramebuffer ()//Backend.createFramebuffer ()

            gl.BindFramebuffer framebufferId
            depthBufferId <- gl.CreateRenderBuffer (width, height)
            gl.BindFramebuffer framebufferId

            gl.BindFramebuffer framebufferId
            gl.BindTexture textureId
            gl.SetFramebufferTexture textureId
            gl.BindFramebuffer 0
            true
        else
            false

    member this.TextureId = textureId

    member this.Release () =
        ()
        // TODO: