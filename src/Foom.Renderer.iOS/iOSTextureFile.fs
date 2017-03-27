namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices

open UIKit
open CoreGraphics
    
type iOSTextureFile (filePath: string) =
    inherit TextureFile ()

    let img = UIImage.FromFile (filePath)

    override this.Width = int <| nfloat.op_Implicit (img.Size.Width)

    override this.Height = int <| nfloat.op_Implicit (img.Size.Height)

    override this.IsTransparent = false

    override this.UseData f =
        ()
        //let bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)

        //f.Invoke bmpData.Scan0

        //bmp.UnlockBits (bmpData)

    override this.Dispose () =
        img.Dispose ()
