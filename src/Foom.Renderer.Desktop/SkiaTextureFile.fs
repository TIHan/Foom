namespace Foom.Renderer

open System
open System.IO
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices

open SkiaSharp
    
type SkiaTextureFile (filePath: string) =
    inherit TextureFile ()

    let fileStream = File.OpenRead (filePath)
    let bmp = SKBitmap.Decode (fileStream)
    let isTransparent = bmp.AlphaType <> SKAlphaType.Opaque

    override this.Width = bmp.Width

    override this.Height = bmp.Height

    override this.IsTransparent = isTransparent

    override this.UseData f =
        f.Invoke (bmp.GetPixels ())

    override this.Dispose () =
        bmp.Dispose ()
        fileStream.Dispose ()
