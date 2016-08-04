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

type RendererState = {
    UniformColor: int<uniform>
    UniformProjection: int<uniform>
    Program: int<program>
    Application: Application
    Vbo: int
    VboLength: int
    DrawVbo: unit -> unit
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
    let wad = Wad.create (System.IO.File.Open ("freedoom1.wad", System.IO.FileMode.Open)) |> Async.RunSynchronously
    let lvl = Wad.findLevel "e1m1" wad |> Async.RunSynchronously

    let app = Renderer.init ()
    let vbo = Renderer.makeVbo ()
    let program = Backend.loadShaders ()
    let uniformColor = Renderer.getUniformColor program
    let uniformProjection = Renderer.getUniformProjection program

    let sectorPolygons =
        lvl.Sectors
        |> Array.map Sector.polygonFlats

    let vertices =
        match sectorPolygons with
        | [||] -> [||]
        | _ ->
            let vertexList =
                sectorPolygons
                |> Array.map (fun x ->
                    let vlist = 
                        x 
                        |> List.map (fun x -> Polygon.vertices x) 
                    match vlist with
                    | [] -> [||]
                    | _ ->
                        vlist
                        |> List.reduce Array.append)

            match vertexList with
            | [||] -> [||]
            | _ ->
                vertexList
                |> Array.reduce Array.append

    Renderer.bufferVbo vertices (sizeof<Vector2> * vertices.Length) vbo

    let index = ref 0
    let arr = ResizeArray<unit -> unit> ()
    sectorPolygons
    |> Array.fold (fun count sector ->
        index := !index + 1
        match sector with
        | [] -> count
        | _ ->
            sector
            |> List.fold (fun count poly ->
                let vertices = Polygon.vertices poly
                arr.Add (fun () -> Renderer.drawArraysLoop count vertices.Length)
                count + vertices.Length) count
    ) 0
    |> ignore        

    let rendererState =
        { UniformColor = uniformColor
          UniformProjection = uniformProjection
          Program = program
          Application = app 
          Vbo = vbo
          VboLength = vertices.Length
          DrawVbo = fun () -> arr.ForEach (fun x -> x ())
          Sectors = sectorPolygons }
    
    { Renderer = rendererState
      User = UserState.Default
      Level = lvl
      ViewDistance = 0.1f
      ViewPosition = Vector3.Zero }

let draw t (prev: ClientState) (curr: ClientState) =
    Renderer.clear ()

    let projection = Matrix4x4.CreatePerspectiveFieldOfView (lerp prev.ViewDistance curr.ViewDistance t, (16.f / 9.f), 0.1f, 100.f) |> Matrix4x4.Transpose
    let model = Matrix4x4.CreateTranslation (lerp prev.ViewPosition curr.ViewPosition t) |> Matrix4x4.Transpose
    let mvp = (projection * model) |> Matrix4x4.Transpose

    Renderer.setUniformProjection curr.Renderer.UniformProjection mvp
    Renderer.setUniformColor curr.Renderer.UniformColor (RenderColor.OfColor Color.White)

    curr.Renderer.DrawVbo () 

    Renderer.draw curr.Renderer.Application