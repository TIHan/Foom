open System
open System.IO
open System.Drawing
open System.Runtime.InteropServices

open Foom.Wad


[<EntryPoint>]
let main argv = 
    let file = File.Open("freedoom1.wad", FileMode.Open)

    let wad = Wad.create file |> Async.RunSynchronously

    let e1m1 = Wad.findLevel "e1m1" wad |> Async.RunSynchronously

    Wad.flats wad
    |> Array.iter (fun tex ->
        let bmp = new Bitmap(64, 64, Imaging.PixelFormat.Format32bppRgb)

        for i = 0 to 64 - 1 do
            for j = 0 to 64 - 1 do
                let pixel = tex.Pixels.[i + (j * 64)]
                bmp.SetPixel (i, j, Drawing.Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))

        bmp.Save(tex.Name + ".bmp")
        bmp.Dispose()
    )

    Wad.loadPatches wad
    |> Array.iter (fun (doomPicture, name) ->
        let bmp = new Bitmap(doomPicture.Width, doomPicture.Height, Imaging.PixelFormat.Format32bppRgb)

        doomPicture.Data
        |> Array2D.iteri (fun i j pixel ->
            bmp.SetPixel (i, j, Drawing.Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))
        )

        bmp.Save (name + ".bmp")
        bmp.Dispose ()
    )

    printfn "done"
    0
