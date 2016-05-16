open System
open System.IO
open System.Drawing
open System.Runtime.InteropServices

open Foom.Wad


[<EntryPoint>]
let main argv = 
    let file = File.Open("freedoom1.wad", FileMode.Open)

    let wad = Wad.create file |> Async.RunSynchronously

    Wad.flats wad
    |> Array.iter (fun tex ->
        let handle = GCHandle.Alloc (tex.Pixels, GCHandleType.Pinned)
        let addr = handle.AddrOfPinnedObject ()
        let bmp = new Bitmap(64, 64, Imaging.PixelFormat.Format32bppRgb)

        for i = 0 to 64 - 1 do
            for j = 0 to 64 - 1 do
                let pixel = tex.Pixels.[i + (j * 64)]
                bmp.SetPixel (i, j, Drawing.Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))

        handle.Free ()
        bmp.Save(tex.Name + ".bmp")
        bmp.Dispose()
    )
    0
