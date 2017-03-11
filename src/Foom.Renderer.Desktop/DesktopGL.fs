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

        member this.BufferData (data: Vector2 [], count, id) =
            let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
            let addr = handle.AddrOfPinnedObject ()
            Backend.bufferData addr count id
            handle.Free ()

        member this.BufferData (data: Vector3 [], count, id) =
            let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
            let addr = handle.AddrOfPinnedObject ()
            Backend.bufferData addr count id
            handle.Free ()

        member this.BufferData (data: Vector4 [], count, id) =
            let handle = GCHandle.Alloc (data, GCHandleType.Pinned)
            let addr = handle.AddrOfPinnedObject ()
            Backend.bufferData addr count id
            handle.Free ()

        member this.BindTexture id =
            Backend.bindTexture id

        member this.CreateTexture (width, height, data) =
            Backend.createTexture width height data

        member this.CreateTextureFromFile (filePath:string) = 1

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

        member this.CreateRenderbuffer (width, height) =
            Backend.createRenderbuffer width height

        member this.GetUniformLocation (programId, name) =
            Backend.getUniformLocation programId name

        member this.BindUniform (locationId, value) =
            Backend.bindUniformInt locationId value

        member this.BindUniform (locationId, value) =
            Backend.bindUniform_float locationId value

        member this.BindUniform (locationId, value) =
            Backend.bindUniformVector2 locationId value

        member this.BindUniform (locationId, value) =
            Backend.bindUniformVector4 locationId value

        member this.BindUniform (locationId, value) =
            Backend.bindUniformMatrix4x4 locationId value

        member this.GetAttributeLocation (programId, name) =
            Backend.getAttributeLocation programId name

        member this.BindAttributePointerFloat32 (locationId, size) =
            Backend.bindVertexAttributePointer_Float locationId size

        member this.EnableAttribute locationId =
            Backend.enableVertexAttribute locationId

        member this.AttributeDivisor (locationId, divisor) =
            Backend.glVertexAttribDivisor locationId divisor

        member this.DrawTriangles (first, count) =
            Backend.drawTriangles first count

        member this.DrawTrianglesInstanced (count, primcount) =
            Backend.drawTrianglesInstanced count primcount

        member this.EnableDepthMask () =
            Backend.depthMaskTrue ()

        member this.DisableDepthMask () =
            Backend.depthMaskFalse ()

        member this.EnableColorMask () =
            Backend.colorMaskTrue ()

        member this.DisableColorMask () =
            Backend.colorMaskFalse ()

        member this.EnableStencilTest () =
            Backend.enableStencilTest ()

        member this.DisableStencilTest () =
            Backend.disableStencilTest ()

        member this.Stencil1 () =
            Backend.stencil1 ()

        member this.Stencil2 () =
            Backend.stencil2 ()

        member this.LoadProgram (vertexBytes, fragmentBytes) =
            Backend.loadShaders vertexBytes fragmentBytes

        member this.UseProgram programId =
            Backend.useProgram programId

        member this.EnableDepthTest () =
            Backend.enableDepth ()

        member this.DisableDepthTest () =
            Backend.disableDepth ()

        member this.Clear () =
            Backend.clear ()

type BitmapTextureFile (filePath: string) =
    inherit TextureFile ()

    let bmp = new Bitmap (filePath)
    let isTransparent = bmp.PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb

    override this.Width = bmp.Width

    override this.Height = bmp.Height

    override this.IsTransparent = isTransparent

    override this.UseData f =
        let bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)

        f.Invoke bmpData.Scan0

        bmp.UnlockBits (bmpData)

    override this.Dispose () =
        bmp.Dispose ()