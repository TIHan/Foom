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
    let doom2Wad = Wad.create (System.IO.File.Open ("doom.wad", System.IO.FileMode.Open)) |> Async.RunSynchronously
    let wad = Wad.createFromWad doom2Wad (System.IO.File.Open ("sunder.wad", System.IO.FileMode.Open)) |> Async.RunSynchronously
    let lvl = Wad.findLevel "e1m1" doom2Wad |> Async.RunSynchronously

    let app = Renderer.init ()
    //let program = Backend.loadShaders ()
    let program = Backend.loadTriangleShader ()
    let uniformColor = Renderer.getUniformColor program
    let uniformProjection = Renderer.getUniformProjection program


    let lookupTexture = Dictionary<string, int> ()

    let flatUnit = 64.f

    Wad.flats doom2Wad
    |> Array.iter (fun tex ->
        let bmp = new Bitmap(64, 64, Imaging.PixelFormat.Format24bppRgb)

        for i = 0 to 64 - 1 do
            for j = 0 to 64 - 1 do
                let pixel = tex.Pixels.[i + (j * 64)]
                bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))

        bmp.Save(tex.Name + ".bmp")
        bmp.Dispose ()

        use ptr = new Gdk.Pixbuf (tex.Name + ".bmp")

        let id = Renderer.createTexture 64 64 (ptr.Pixels)

        lookupTexture.Add (tex.Name, id)
    )

    Wad.flats wad
    |> Array.iter (fun tex ->
        let bmp = new Bitmap(64, 64, Imaging.PixelFormat.Format24bppRgb)

        for i = 0 to 64 - 1 do
            for j = 0 to 64 - 1 do
                let pixel = tex.Pixels.[i + (j * 64)]
                bmp.SetPixel (i, j, Color.FromArgb (int pixel.R, int pixel.G, int pixel.B))

        bmp.Save(tex.Name + ".bmp")
        bmp.Dispose ()

        use ptr = new Gdk.Pixbuf (tex.Name + ".bmp")

        let id = Renderer.createTexture 64 64 (ptr.Pixels)

        if lookupTexture.ContainsKey (tex.Name) |> not then
            lookupTexture.Add (tex.Name, id)
    )

    let sectorPolygons =
        lvl.Sectors
        //[| lvl.Sectors.[663] |]
        |> Array.mapi (fun i s -> 
            System.Diagnostics.Debug.WriteLine ("Sector " + string i)
            (Sector.polygonFlats s, s.FloorTextureName, s.LightLevel)
        )

    let vbos = ResizeArray ()
    let textureVbosUV = ResizeArray ()

    sectorPolygons
    |> Array.iter (fun (polygons, floorTextureName, lightLevel) ->

        polygons
        |> List.iter (fun polygon ->
            let vertices =
                polygon
                |> Array.map (fun x -> [|x.X;x.Y;x.Z|])
                |> Array.reduce Array.append
                |> Array.map (fun x -> Vector3 (x.X, x.Y, 0.f))

            let uv = Array.zeroCreate vertices.Length

            let mutable i = 0
            while (i < vertices.Length) do
                let p1 = vertices.[i]
                let p2 = vertices.[i + 1]
                let p3 = vertices.[i + 2]

                uv.[i] <- Vector2 (p1.X / flatUnit, p1.Y / flatUnit * -1.f)
                uv.[i + 1] <- Vector2(p2.X / flatUnit, p2.Y / flatUnit * -1.f)
                uv.[i + 2] <- Vector2(p3.X / flatUnit, p3.Y / flatUnit * -1.f)

                i <- i + 3

            let uvVbo = Renderer.makeVbo ()
            Renderer.bufferVbo uv (sizeof<Vector2> * uv.Length) uvVbo
  
            textureVbosUV.Add uvVbo

            let vbo = Renderer.makeVbo ()
            Renderer.bufferVboVector3 vertices (sizeof<Vector3> * vertices.Length) vbo

            let textureId = lookupTexture.[floorTextureName]


            let lightLevel =
                if lightLevel > 255 then 255
                else lightLevel

            vbos.Add 
                {
                    Id = vbo
                    Length = vertices.Length
                    Color = Color.FromArgb(lightLevel, lightLevel, lightLevel)
                    TextureId = textureId
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
            Renderer.setUniformColor uniformColor (RenderColor.OfColor Color.Red)

            Renderer.bindVbo vbo.Id
            Renderer.bindPosition program

            Renderer.bindVbo vbo.UvVbo
            Renderer.bindUv program

            Renderer.bindTexture vbo.TextureId

            for i = 0 to (vbo.Length / 3) do

                Renderer.drawArraysLoop (i * 3) 3

            Renderer.setUniformColor uniformColor (RenderColor.OfColor vbo.Color)
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

    let position =
        match sectorPolygons.[0] with
        | (polygons, _, _) -> polygons.[0].[0].X
    
    { Renderer = rendererState
      User = UserState.Default
      Level = lvl
      ViewDistance = 1.f
      ViewPosition = new Vector3 (-position.X, -position.Y, flatUnit * -1.f * 32.f) }

let draw t (prev: ClientState) (curr: ClientState) =
    Renderer.clear ()

    let projection = Matrix4x4.CreatePerspectiveFieldOfView (lerp prev.ViewDistance curr.ViewDistance t, (16.f / 9.f), 1.f, System.Single.MaxValue) |> Matrix4x4.Transpose
    let model = Matrix4x4.CreateTranslation (lerp prev.ViewPosition curr.ViewPosition t) |> Matrix4x4.Transpose
    let mvp = (projection * model) |> Matrix4x4.Transpose

    Renderer.enableDepth ()
    curr.Renderer.DrawVbo mvp
    Renderer.disableDepth ()

    Renderer.draw curr.Renderer.Application