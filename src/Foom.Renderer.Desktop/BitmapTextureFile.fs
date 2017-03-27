namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices
    
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
