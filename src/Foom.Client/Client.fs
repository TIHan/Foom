[<RequireQualifiedAccess>]
module Foom.Client.Client

open System.Drawing
open System.Numerics

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
    }

type RendererState = {
    UniformColor: int<uniform>
    UniformProjection: int<uniform>
    Program: int<program>
    Application: Application
    Vbos: Vbo ResizeArray
    DrawVbo: Matrix4x4 -> unit
    Sectors: Polygon list [] }

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

    let sectorPolygons =
        lvl.Sectors
        |> Array.map Sector.polygonFlats

    //let vertices =
    //    match sectorPolygons with
    //    | [||] -> [||]
    //    | _ ->
    //        let vertexList =
    //            sectorPolygons
    //            |> Array.map (fun x ->
    //                let vlist = 
    //                    x 
    //                    |> List.map (fun x -> Polygon.vertices x) 
    //                match vlist with
    //                | [] -> [||]
    //                | _ ->
    //                    vlist
    //                    |> List.reduce Array.append)

    //        match vertexList with
    //        | [||] -> [||]
    //        | _ ->
    //            vertexList
    //            |> Array.reduce Array.append


    let vbos = ResizeArray ()

    let random = System.Random ()

    sectorPolygons
    |> Array.iter (fun polygons ->
        let color = Color.FromArgb(random.Next(0, 255), random.Next(0, 255), random.Next (0, 255))

        polygons
        |> List.iter (fun (Polygon vertices: Polygon) ->
            let vertices =
                vertices
                |> Array.map (fun x -> Vector3 (x.X, x.Y, 0.f))

            let vbo = Renderer.makeVbo ()
            Renderer.bufferVboVector3 vertices (sizeof<Vector3> * vertices.Length) vbo
            vbos.Add 
                {
                    Id = vbo
                    Length = vertices.Length
                    Color = color
                }

        )
    )

    let index = ref 0
    let arr = ResizeArray<Matrix4x4 -> unit> ()    

    vbos
    |> Seq.iter (fun vbo ->
        fun mvp ->
            Renderer.bindVbo vbo.Id
            Renderer.setUniformProjection uniformProjection mvp

            Renderer.setUniformColor uniformColor (RenderColor.OfColor vbo.Color)
            Renderer.bindPosition program
            Renderer.drawTriangleStrip 0 vbo.Length
        |> arr.Add
    )

    let rendererState =
        { UniformColor = uniformColor
          UniformProjection = uniformProjection
          Program = program
          Application = app 
          Vbos = vbos
          DrawVbo = fun m -> arr.ForEach (fun x -> x m)
          Sectors = sectorPolygons }

    let (Polygon vertices) = sectorPolygons.[28].[0]
    
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