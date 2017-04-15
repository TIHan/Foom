namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices

#if __IOS__ || __ANDROID__
open OpenTK.Graphics.ES30
#else
open OpenTK.Graphics.OpenGL
#endif

open FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module OpenTKGL =

    let loadShaders (vertexSource : string) (fragmentSource : string) : int =
#if __IOS__ || __ANDROID__
        let vertexSource = vertexSource.Replace ("#version 330 core", "#version 300 es")
        let fragmentSource = fragmentSource.Replace ("#version 330 core", "#version 300 es")
#endif
        let vertexShaderId = GL.CreateShader (ShaderType.VertexShader)
        let fragmentShaderId = GL.CreateShader (ShaderType.FragmentShader)

        let mutable result = 0
        let mutable infoLogLength = 0

        // Compile Vertex Shader
        GL.ShaderSource (vertexShaderId, vertexSource)
        GL.CompileShader (vertexShaderId)

        // Check Vertex Shader
        GL.GetShader (vertexShaderId, ShaderParameter.CompileStatus, &result)
        GL.GetShader (vertexShaderId, ShaderParameter.InfoLogLength, &infoLogLength)
        if infoLogLength > 0 then
            let log = GL.GetShaderInfoLog (vertexShaderId)
            printfn "%s" log

        // Compile Fragment Shader
        GL.ShaderSource (fragmentShaderId, fragmentSource)
        GL.CompileShader (fragmentShaderId)

        // Check Fragment Shader
        GL.GetShader (fragmentShaderId, ShaderParameter.CompileStatus, &result)
        GL.GetShader (fragmentShaderId, ShaderParameter.InfoLogLength, &infoLogLength)
        if infoLogLength > 0 then
            let log = GL.GetShaderInfoLog (fragmentShaderId)
            printfn "%s" log


        printfn "Linking program."
        let programId = GL.CreateProgram ()
        GL.AttachShader (programId, vertexShaderId)
        GL.AttachShader (programId, fragmentShaderId)
        GL.LinkProgram programId

        // Check Program
#if __IOS__ || __ANDROID__
#else
        GL.GetProgram (programId, GetProgramParameterName.LinkStatus, &result)
        GL.GetProgram (programId, GetProgramParameterName.InfoLogLength, &infoLogLength)
#endif
        if infoLogLength > 0 then
            let log = GL.GetProgramInfoLog (programId)
            printfn "%s" log
        
        programId

    let checkError () =
        #if __IOS__ || __ANDROID__
        let errorCode = GL.GetErrorCode ()// GetError ()
        if errorCode <> ErrorCode.NoError then
            printfn "GL ERROR: %A" errorCode
        #else
        let errorCode = GL.GetError ()
        if errorCode <> ErrorCode.NoError then
            printfn "GL ERROR: %A" errorCode
        #endif

    let getMaxTextureSize () =
        let mutable max = 0
        GL.GetInteger (GetPName.MaxTextureSize, &max)
        max

    let checkTextureSize width height =
        let maxTextureSize = getMaxTextureSize ()
        if width > maxTextureSize || height > maxTextureSize then
            printfn "GL WARNING: Texture dimension, (%i, %i), is bigger than max texture size, %i." width height maxTextureSize

    let checkFramebufferStatus () =
        let status = GL.CheckFramebufferStatus (FramebufferTarget.Framebuffer)
        if status <> FramebufferErrorCode.FramebufferComplete then 
            printfn "Framebuffer Status: %A" status

type OpenTKGL (swapBuffers) =

    interface IGL with

        member this.BindBuffer id =
            GL.BindBuffer (BufferTarget.ArrayBuffer, id)
            checkError ()

        member this.CreateBuffer () =
            let mutable id = 0
            GL.GenBuffers (1, &id)
            checkError ()
            id

        member this.DeleteBuffer id =
            let mutable id = id
            GL.DeleteBuffers (1, &id)
            checkError ()

        member this.BufferData (data: Vector2 [], count : int, id) =
            let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
            let addr = handle.AddrOfPinnedObject ()

            GL.BindBuffer (BufferTarget.ArrayBuffer, uint32 id)
#if __IOS__ || __ANDROID__
            GL.BufferData (BufferTarget.ArrayBuffer, (nativeint count), data, BufferUsage.DynamicDraw)
#else
            GL.BufferData (BufferTarget.ArrayBuffer, count, addr, BufferUsageHint.DynamicDraw)
#endif

            handle.Free ()
            checkError ()

        member this.BufferData (data: Vector3 [], count : int, id) =
            let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
            let addr = handle.AddrOfPinnedObject ()

            GL.BindBuffer (BufferTarget.ArrayBuffer, uint32 id)
#if __IOS__ || __ANDROID__
            GL.BufferData (BufferTarget.ArrayBuffer, (nativeint count), data, BufferUsage.DynamicDraw)
#else
            GL.BufferData (BufferTarget.ArrayBuffer, count, addr, BufferUsageHint.DynamicDraw)
#endif

            handle.Free ()
            checkError ()

        member this.BufferData (data: Vector4 [], count : int, id) =
            let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
            let addr = handle.AddrOfPinnedObject ()

            GL.BindBuffer (BufferTarget.ArrayBuffer, uint32 id)
#if __IOS__ || __ANDROID__
            GL.BufferData (BufferTarget.ArrayBuffer, (nativeint count), data, BufferUsage.DynamicDraw)
#else
            GL.BufferData (BufferTarget.ArrayBuffer, count, addr, BufferUsageHint.DynamicDraw)
#endif

            handle.Free ()
            checkError ()

        member this.BindTexture id =
            GL.BindTexture (TextureTarget.Texture2D, id)
            checkError ()

        member this.ActiveTexture number =
#if __ANDROID__
            let textureUnit : TextureUnit = LanguagePrimitives.EnumOfValue (int TextureUnit.Texture0 + number)
            GL.ActiveTexture textureUnit
#else
            GL.ActiveTexture (LanguagePrimitives.EnumOfValue (int TextureUnit.Texture0 + number))
#endif
            checkError ()

        member this.CreateTexture (width : int, height : int, data : nativeint) =
            let textureID = GL.GenTexture ()

            GL.BindTexture (TextureTarget.Texture2D, textureID)
            checkError ()

            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
            checkError ()
            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
            checkError ()
            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureWrapS, int TextureWrapMode.Repeat)
            checkError ()
            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureWrapT, int TextureWrapMode.Repeat)
            checkError ()

            checkTextureSize width height

            GL.TexImage2D (TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data)
            checkError ()

            textureID

        member this.SetSubTexture (xOffset, yOffset, width, height, data, textureId) =
            GL.BindTexture (TextureTarget.Texture2D, textureId)
            checkError ()
            GL.TexSubImage2D (TextureTarget.Texture2D, 0, xOffset, yOffset, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, data)
            checkError ()
            GL.BindTexture (TextureTarget.Texture2D, 0)
            checkError ()

        member this.DeleteTexture id =
            GL.DeleteTexture (uint32 id)
            checkError ()

        member this.BindFramebuffer id =
            GL.BindFramebuffer (FramebufferTarget.Framebuffer, id)
            checkError ()

        member this.CreateFramebuffer () =
            let mutable id = 0
            GL.GenFramebuffers (1, &id)
            checkError ()
            id

        member this.CreateFramebufferTexture (width, height, data) =
            let textureID = GL.GenTexture ()

            GL.BindTexture (TextureTarget.Texture2D, textureID)
            checkError ()

            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
            checkError ()
            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
            checkError ()
            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
            checkError ()
            GL.TexParameter (TextureTarget.Texture2D, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
            checkError ()

            checkTextureSize width height

            GL.TexImage2D (TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data)
            checkError ()

            textureID

        member this.SetFramebufferTexture id =
#if __IOS__ || __ANDROID__
            GL.FramebufferTexture2D (FramebufferTarget.Framebuffer, FramebufferSlot.ColorAttachment0, TextureTarget.Texture2D, id, 0)
            checkError ()
            let mutable drawBuffers = DrawBufferMode.ColorAttachment0
            GL.DrawBuffers (1, &drawBuffers)
            checkError ()
#else
            GL.FramebufferTexture (FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, id, 0)
            let mutable drawBuffers = DrawBuffersEnum.ColorAttachment0
            GL.DrawBuffers (1, &drawBuffers)
#endif

        member this.CreateRenderbuffer (width, height) =
            let mutable depthrenderbuffer = 0
            GL.GenRenderbuffers (1, &depthrenderbuffer)
            checkError ()
            GL.BindRenderbuffer (RenderbufferTarget.Renderbuffer, depthrenderbuffer)
            checkError ()
#if __IOS__ || __ANDROID__
            GL.RenderbufferStorage (RenderbufferTarget.Renderbuffer, RenderbufferInternalFormat.Depth32FStencil8, width, height)
            checkError ()
            GL.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, FramebufferSlot.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthrenderbuffer)
            checkError ()
#else
            GL.RenderbufferStorage (RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth32fStencil8, width, height)
            checkError ()
            GL.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthrenderbuffer)
            checkError ()
#endif
            GL.BindRenderbuffer (RenderbufferTarget.Renderbuffer, 0)
            checkError ()
            depthrenderbuffer

        member this.GetUniformLocation (programId, name) =
            GL.GetUniformLocation (programId, name)

        member this.BindUniform (locationId, value : int) =
            GL.Uniform1 (locationId, value)
            checkError ()

        member this.BindUniform (locationId, count, values: int []) =
            let handle = GCHandle.Alloc (values, GCHandleType.Pinned)
            let addr = handle.AddrOfPinnedObject ()
            let ptr : nativeptr<int> = addr |> NativePtr.ofNativeInt
            GL.Uniform1 (locationId, count, ptr)
            handle.Free ()
            checkError ()

        member this.BindUniform (locationId, value : single) =
            GL.Uniform1 (locationId, value)
            checkError ()

        member this.BindUniform (locationId, value : Vector2) =
            GL.Uniform2 (locationId, value.X, value.Y)
            checkError ()

        member this.BindUniform (locationId, value : Vector4) =
            GL.Uniform4 (locationId, value.X, value.Y, value.Z, value.W)
            checkError ()

        member this.BindUniform (locationId, value : Matrix4x4) =
            // there may be a bug here
            let mutable value = value
            let ptr : nativeptr<single> = &&value |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
            GL.UniformMatrix4 (locationId, 1, false, ptr)
            checkError ()

        member this.GetAttributeLocation (programId, name) =
            GL.GetAttribLocation (programId, name)

        member this.BindAttributePointerFloat32 (locationId, size) =
            GL.VertexAttribPointer (locationId, size, VertexAttribPointerType.Float, false, 0, 0)
            checkError ()

        member this.EnableAttribute locationId =
            GL.EnableVertexAttribArray locationId
            checkError ()

        member this.AttributeDivisor (locationId, divisor) =
            GL.VertexAttribDivisor (locationId, divisor)
            checkError ()

        member this.DrawTriangles (first, count) =
#if __IOS__ || __ANDROID__
            GL.DrawArrays (BeginMode.Triangles, first, count)
#else
            GL.DrawArrays (PrimitiveType.Triangles, first, count)
#endif
            checkError ()

        member this.DrawTrianglesInstanced (count, primcount) =
            GL.DrawArraysInstanced (PrimitiveType.Triangles, 0, count, primcount)
            checkError ()

        member this.EnableDepthMask () =
            GL.DepthMask true
            checkError ()

        member this.DisableDepthMask () =
            GL.DepthMask false
            checkError ()

        member this.EnableColorMask () =
            GL.ColorMask (true, true, true, true)
            checkError ()

        member this.DisableColorMask () =
            GL.ColorMask (false, false, false, false)
            checkError ()

        member this.EnableStencilTest () =
            GL.Enable (EnableCap.StencilTest)
            checkError ()

        member this.DisableStencilTest () =
            GL.Disable (EnableCap.StencilTest)
            checkError ()

        member this.Stencil1 () =
            GL.StencilFunc (StencilFunction.Always, 1, 0xFF)
            GL.StencilOp (StencilOp.Keep, StencilOp.Keep, StencilOp.Replace)
            GL.StencilMask 0xFF
            GL.Clear (ClearBufferMask.StencilBufferBit)
            checkError ()

        member this.Stencil2 () =
            GL.StencilFunc (StencilFunction.Equal, 1, 0xFF)
            GL.StencilMask (0x00)
            checkError ()

        member this.LoadProgram (vertexSource, fragmentSource) =
            loadShaders vertexSource fragmentSource

        member this.UseProgram programId =
            GL.UseProgram programId
            checkError ()

        member this.EnableDepthTest () =
            GL.Enable (EnableCap.DepthTest)
            GL.DepthFunc (DepthFunction.Less)
            GL.Enable (EnableCap.CullFace)
            checkError ()

        member this.DisableDepthTest () =
            GL.Disable (EnableCap.CullFace)
            GL.Disable (EnableCap.DepthTest)
            checkError ()

        member this.Clear () =
            checkFramebufferStatus ()
            GL.Clear (ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
            checkError ()

        member this.Swap () =
            checkError ()
            swapBuffers ()
