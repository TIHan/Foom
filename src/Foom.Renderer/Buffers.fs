namespace Foom.Renderer

open System
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices

// *****************************************
// *****************************************
// Array Buffers
// *****************************************
// *****************************************

[<Sealed>]
type Buffer<'T when 'T : struct> internal (data: 'T [], bufferData: IGL -> 'T [] -> int -> int -> unit) =

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

type Buffer private () =

    static member Create (data) =
        Buffer<Vector2> (data, fun gl data size id -> gl.BufferData (data, (sizeof<Vector2> * size), id))

    static member Create (data) =
        Buffer<Vector3> (data, fun gl data size id -> gl.BufferData (data, (sizeof<Vector3> * size), id))

    static member Create (data) =
        Buffer<Vector4> (data, fun gl data size id -> gl.BufferData (data, (sizeof<Vector4> * size), id))

type Texture2DBufferQueueItem =
    | Empty
    | Data of byte []
    | File of TextureFile
    | Files of TextureFile list * width: int * height: int

type Texture2DBuffer () =

    let mutable id = 0
    let mutable width = 0
    let mutable height = 0
    let mutable queuedData = Empty
    let mutable isTransparent = false

    member this.Id = id

    member this.Width = width

    member this.Height = height

    member this.HasData = id > 0 || queuedData <> Empty

    member this.Set (file: TextureFile) =
        width <- file.Width
        height <- file.Height
        queuedData <- File file

    member this.Set (files: TextureFile list, width', height') =
        width <- width'
        height <- height'
        queuedData <- Files (files, width', height')

    member this.Bind (gl: IGL) =
        if id <> 0 then
            gl.BindTexture id 

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

        | File file ->

            file.UseData (fun data -> id <- gl.CreateTexture (file.Width, file.Height, data))

            file.Dispose ()
            queuedData <- Empty
            true

        | Files (files, width, height) ->

            id <- gl.CreateTexture (width, height, nativeint 0)

            let mutable xOffset = 0

            files
            |> List.iter (fun file ->
                file.UseData (fun data -> 
                    gl.SetSubTexture (xOffset, 0, file.Width, file.Height, data, id)
                )

                xOffset <- xOffset + file.Width

                file.Dispose ()
            )

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

    member this.Unbind (gl: IGL) =
        gl.BindFramebuffer 0

    member this.BindTexture (gl: IGL) =
        if textureId <> 0 then
            gl.BindTexture textureId

    member this.TryBufferData (gl: IGL) =
        
        if framebufferId = 0 then
            textureId <- gl.CreateFramebufferTexture (width, height, nativeint 0)
            framebufferId <- gl.CreateFramebuffer ()

            gl.BindFramebuffer framebufferId
            depthBufferId <- gl.CreateRenderbuffer (width, height)
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