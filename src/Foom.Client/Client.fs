[<RequireQualifiedAccess>]
module Foom.Client.Client

open System.Drawing
open System.Numerics
open System.Collections.Generic

open Foom.Renderer
open Foom.Wad
open Foom.Wad.Geometry
open Foom.Wad.Level
open Foom.Wad.Level.Structures

let inline lerp x y t = x + (y - x) * t

type UserState = {
    IsMapMoving: bool } with

    static member Default = { IsMapMoving = false }

type Vbo =
    {
        Id: int
        Length: int
        Color: Color
        TextureId: int
        UvVbo: int
    }

type RendererState = {
    UniformColor: int<uniform>
    UniformProjection: int<uniform>
    Program: int<program>
    Application: Application
    Vbos: Vbo ResizeArray
    DrawVbo: Matrix4x4 -> unit }

type ClientState = {
    Renderer: RendererState
    User: UserState
    Level: Level
    ViewDistance: single
    ViewPosition: Vector3 }

// These are sectors to look for and test to ensure things are working as they should.
// 568 - map10 sunder
// 4 - map10  sunder
// 4371 - map14 sunder
// 28 - e1m1 doom
// 933 - map10 sunder
// 20 - map10 sunder
// 151 - map10 sunder
// 439 - map08 sunder
// 271 - map03 sunder

let init () =
    let wad = Wad.create (System.IO.File.Open ("doom.wad", System.IO.FileMode.Open)) |> Async.RunSynchronously
    let lvl = Wad.findLevel "e1m1" wad |> Async.RunSynchronously

    let app = Renderer.init ()
    //let program = Backend.loadShaders ()
    let program = Backend.loadTriangleShader ()
    let uniformColor = Renderer.getUniformColor program
    let uniformProjection = Renderer.getUniformProjection program


    let lookupTexture = Dictionary<string, int> ()


    Wad.flats wad
    |> Array.iter (fun tex ->
        let bmp = new Bitmap(64, 64, Imaging.PixelFormat.Format32bppRgb)

        for i = 0 to 64 - 1 do
            for j = 0 to 64 - 1 do
                let pixel = tex.Pixels.[i + (j * 64)]
                bmp.SetPixel (i, j, Color.Green) // Color.FromArgb (int pixel.R, int pixel.G, int pixel.B)

        let rect = new Rectangle(0, 0, bmp.Width, bmp.Height)
        let bmpData = bmp.LockBits (rect, Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppRgb)

        let id = Renderer.createTexture 64 64 (bmpData.Scan0)

        bmp.UnlockBits (bmpData)

        bmp.Dispose ()

        lookupTexture.Add (tex.Name, id)
    )



    let sectorPolygons =
        lvl.Sectors
        |> Array.map (fun s -> (Sector.polygonFlats s, s.FloorTextureName))

    let vbos = ResizeArray ()
    let textureVbosUV = ResizeArray ()

    let random = System.Random ()

    sectorPolygons
    |> Array.iter (fun (polygons, floorTextureName) ->
        let color = Color.FromArgb(random.Next(0, 255), random.Next(0, 255), random.Next (0, 255))

        polygons
        |> List.iter (fun polygon ->
            let vertices =
                Polygon.vertices polygon
                |> Array.map (fun x -> Vector3 (x.X, x.Y, 0.f))

            let uv = Array.zeroCreate vertices.Length

            let mutable i = 0
            while (i < vertices.Length) do
                let p1 = vertices.[i]
                let p2 = vertices.[i + 1]
                let p3 = vertices.[i + 2]

                uv.[i] <- Vector2 (0.f, 1.f)
                uv.[i] <- Vector2 (1.f, 1.f)
                uv.[i] <- Vector2 (1.f, 0.f)

                i <- i + 3

            let uvVbo = Renderer.makeVbo ()
            Renderer.bufferVbo uv (sizeof<Vector2> * uv.Length) uvVbo
  
            textureVbosUV.Add uvVbo

            let vbo = Renderer.makeVbo ()
            Renderer.bufferVboVector3 vertices (sizeof<Vector3> * vertices.Length) vbo
            vbos.Add 
                {
                    Id = vbo
                    Length = vertices.Length
                    Color = color
                    TextureId = lookupTexture.[floorTextureName]
                    UvVbo = uvVbo
                }

        )
    )

    let index = ref 0
    let arr = ResizeArray<Matrix4x4 -> unit> ()    

    vbos
    |> Seq.iter (fun vbo ->
        fun mvp ->
            Renderer.setUniformProjection uniformProjection mvp
            Renderer.setTexture program vbo.TextureId
            Renderer.setUniformColor uniformColor (RenderColor.OfColor vbo.Color)

            Renderer.bindVbo vbo.Id
            Renderer.bindPosition program

            Renderer.bindVbo vbo.UvVbo
            Renderer.bindUv program

            Renderer.drawTriangles 0 vbo.Length
        |> arr.Add
    )

    let rendererState =
        { UniformColor = uniformColor
          UniformProjection = uniformProjection
          Program = program
          Application = app 
          Vbos = vbos
          DrawVbo = fun m -> arr.ForEach (fun x -> x m) }
    
    { Renderer = rendererState
      User = UserState.Default
      Level = lvl
      ViewDistance = 0.1f
      ViewPosition = Vector3(-0.05f, 0.05f, 0.f) }

let draw t (prev: ClientState) (curr: ClientState) =
    Renderer.clear ()

    let projection = Matrix4x4.CreatePerspectiveFieldOfView (lerp prev.ViewDistance curr.ViewDistance t, (16.f / 9.f), 0.1f, 100.f) |> Matrix4x4.Transpose
    let model = Matrix4x4.CreateTranslation (lerp prev.ViewPosition curr.ViewPosition t) |> Matrix4x4.Transpose
    let mvp = (projection * model) |> Matrix4x4.Transpose

    curr.Renderer.DrawVbo mvp

    Renderer.draw curr.Renderer.Application