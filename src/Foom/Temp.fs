module Foom.Export

open System
open System.IO

open Foom.Wad

open SkiaSharp

let savePng name (pixels : Pixel [,]) =
    let mutable isTransparent = false

    let width = Array2D.length1 pixels
    let height = Array2D.length2 pixels

    pixels
    |> Array2D.iter (fun p ->
        if p.Equals Pixel.Cyan then
            isTransparent <- true
    )

    use bitmap = new SKBitmap (width, height, not isTransparent)
    for i = 0 to width - 1 do
        for j = 0 to height - 1 do
            let pixel = pixels.[i, j]
            if pixel = Pixel.Cyan then
                bitmap.SetPixel (i, j, SKColor (0uy, 0uy, 0uy, 0uy))
            else
                bitmap.SetPixel (i, j, SKColor (pixel.R, pixel.G, pixel.B))

    use image = SKImage.FromBitmap (bitmap)
    use data = image.Encode ()
    use fs = File.OpenWrite (name)

    data.SaveTo (fs)

let exportFlatTextures (wad: Wad) =
    wad
    |> Wad.iterFlatTextureName (fun name ->
        Wad.tryFindFlatTexture name wad
        |> Option.iter (fun tex ->
            let name = tex.Name + "_flat.png"
            savePng name tex.Data
        )
    )

let exportTextures (wad: Wad) =
    wad
    |> Wad.iterTextureName (fun name ->
        Wad.tryFindTexture name wad
        |> Option.iter (fun tex ->
            let name = tex.Name + ".png"
            savePng name tex.Data
        )
    )

let exportSpriteTextures (wad: Wad) =
    wad
    |> Wad.iterSpriteTextureName (fun name ->
        Wad.tryFindSpriteTexture name wad
        |> Option.iter (fun tex ->
            let name = tex.Name + ".png"
            savePng name tex.Data
        )
    )