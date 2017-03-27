module Foom.Export

open System
open System.IO
open System.Numerics
open System.Drawing
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Physics
open Foom.Renderer
open Foom.Geometry
open Foom.Wad
open Foom.Client.Sky
open Foom.Renderer.RendererSystem

open Foom.Game.Core
open Foom.Game.Assets
open Foom.Game.Sprite
open Foom.Game.Level
open Foom.Game.Wad
open Foom.Game.Gameplay.Doom

#if __IOS__
#else
let exportFlatTextures (wad: Wad) =
    wad
    |> Wad.iterFlatTextureName (fun name ->
        Wad.tryFindFlatTexture name wad
        |> Option.iter (fun tex ->
            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iter (fun p ->
                if p.Equals Pixel.Cyan then
                    isTransparent <- true
            )

            let format =
                if isTransparent then
                    Imaging.PixelFormat.Format32bppArgb
                else
                    Imaging.PixelFormat.Format24bppRgb

            let bmp = new Bitmap(width, height, format)

            tex.Data
            |> Array2D.iteri (fun i j pixel ->
                if pixel = Pixel.Cyan then
                    bmp.SetPixel (i, j, Color.FromArgb (0, 0, 0, 0))
                else
                    bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))
            )

            bmp.Save (tex.Name + "_flat.bmp")
            bmp.Dispose ()
        )
    )

let exportTextures (wad: Wad) =
    wad
    |> Wad.iterTextureName (fun name ->
        Wad.tryFindTexture name wad
        |> Option.iter (fun tex ->
            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iter (fun p ->
                if p.Equals Pixel.Cyan then
                    isTransparent <- true
            )

            let format =
                if isTransparent then
                    Imaging.PixelFormat.Format32bppArgb
                else
                    Imaging.PixelFormat.Format24bppRgb

            let bmp = new Bitmap(width, height, format)

            tex.Data
            |> Array2D.iteri (fun i j pixel ->
                if pixel = Pixel.Cyan then
                    bmp.SetPixel (i, j, Color.FromArgb (0, 0, 0, 0))
                else
                    bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))
            )

            bmp.Save (tex.Name + ".bmp")
            bmp.Dispose ()
        )
    )

let exportSpriteTextures (wad: Wad) =
    wad
    |> Wad.iterSpriteTextureName (fun name ->
        Wad.tryFindSpriteTexture name wad
        |> Option.iter (fun tex ->
            let width = Array2D.length1 tex.Data
            let height = Array2D.length2 tex.Data

            let mutable isTransparent = false

            tex.Data
            |> Array2D.iter (fun p ->
                if p.Equals Pixel.Cyan then
                    isTransparent <- true
            )

            let format =
                if isTransparent then
                    Imaging.PixelFormat.Format32bppArgb
                else
                    Imaging.PixelFormat.Format24bppRgb

            let bmp = new Bitmap(width, height, format)

            tex.Data
            |> Array2D.iteri (fun i j pixel ->
                if pixel = Pixel.Cyan then
                    bmp.SetPixel (i, j, Color.FromArgb (0, 0, 0, 0))
                else
                    bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))
            )

            bmp.Save (tex.Name + ".bmp")
            bmp.Dispose ()
        )
    )
#endif