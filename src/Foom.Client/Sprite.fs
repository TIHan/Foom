﻿module Foom.Client.Sprite

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

open Foom.Common.Components
open Foom.Ecs
open Foom.Renderer
open Foom.Collections
open Foom.Renderer.RendererSystem

[<Sealed>]
type Sprite (center, positions) =
    inherit GpuResource ()

    member val Center = Buffer.createVector3 center

    member val Positions = Buffer.createVector3 positions

let createSpriteVertices width height =
    let halfWidth = single width / 2.f

    [|
        Vector3 (-halfWidth, 0.f, 0.f)
        Vector3 (halfWidth, 0.f, 0.f)
        Vector3 (halfWidth, 0.f, single height)
        Vector3 (halfWidth, 0.f, single height)
        Vector3 (-halfWidth, 0.f, single height)
        Vector3 (-halfWidth, 0.f, 0.f)
    |]

let createSpriteCenter (vertices: Vector3 []) =
    vertices
    |> Seq.chunkBySize 6
    |> Seq.map (fun quadVerts ->
        let min = 
            quadVerts
            |> Array.sortBy (fun x -> x.X)
            |> Array.sortBy (fun x -> x.Z)
            |> Array.head
        let max =
            quadVerts
            |> Array.sortByDescending (fun x -> x.X)
            |> Array.sortByDescending (fun x -> x.Z)
            |> Array.head
        let mid = min + ((max - min) / 2.f)
        Array.init quadVerts.Length (fun _ -> mid)
    )
    |> Seq.reduce Array.append

let createSpriteColor lightLevel =
    let color = Array.init 6 (fun _ -> Color.FromArgb(255, int lightLevel, int lightLevel, int lightLevel))
    color
    |> Array.map (fun c ->
        Vector4 (
            single c.R / 255.f,
            single c.G / 255.f,
            single c.B / 255.f,
            single c.A / 255.f)
    )

let uv =
    [|
        Vector2 (0.f, 0.f * -1.f)
        Vector2 (1.f, 0.f * -1.f)
        Vector2 (1.f, 1.f * -1.f)
        Vector2 (1.f, 1.f * -1.f)
        Vector2 (0.f, 1.f * -1.f)
        Vector2 (0.f, 0.f * -1.f)
    |]

type SpriteRendererComponent (subRenderer, texture, lightLevel) =
    inherit RenderComponent<Sprite> (subRenderer, texture, Mesh ([||], uv, createSpriteColor lightLevel), Sprite ([||], [||]))

    member val SpriteCount = 0 with get, set

    member val Positions : Vector3 [] = Array.zeroCreate 10000

[<Sealed>]
type SpriteComponent (subRenderer: string, texture: string) =
    inherit Component ()

    member val SubRenderer = subRenderer

    member val Texture = texture

    member val SetPosition : Vector3 -> unit = fun pos -> () with get, set

let handleSprite () =
    let lookup = Dictionary<string * string, Entity * SpriteRendererComponent> ()
    let textureLookup = Dictionary<string, int * int> ()
    Behavior.merge
        [

            Behavior.handleComponentAdded (fun ent (comp: SpriteComponent) _ em ->
                let _, rendererComp = 
                    let key = (comp.SubRenderer.ToUpper (), comp.Texture.ToUpper ())
                    match lookup.TryGetValue (key) with
                    | true, x -> x
                    | _ ->
                        let rendererComp = new SpriteRendererComponent(comp.SubRenderer, comp.Texture, 255)

                        let rendererEnt = em.Spawn ()
                        em.Add (rendererEnt, rendererComp)

                        lookup.[key] <- (rendererEnt, rendererComp)

                        rendererEnt, rendererComp
                    
                comp.SetPosition <- fun pos ->
                    if rendererComp.SpriteCount < rendererComp.Positions.Length then
                        rendererComp.Positions.[rendererComp.SpriteCount] <- pos
                        rendererComp.SpriteCount <- rendererComp.SpriteCount + 1
                    
            )

            Behavior.handleComponentAdded (fun ent (comp: SpriteRendererComponent) _ em ->
                let width, height =
                    match textureLookup.TryGetValue (comp.Texture.ToUpper ()) with
                    | true, x -> x
                    | _ ->
                        use bmp = new Bitmap(comp.Texture)
                        let x = (bmp.Width, bmp.Height)
                        textureLookup.[comp.Texture.ToUpper()] <- x
                        x

                let vertices = createSpriteVertices width height
                let center = createSpriteCenter vertices

                comp.Mesh.Position.Set vertices
                comp.Extra.Center.Set center
            )

            Behavior.update (fun _ em ea ->
                em.ForEach<TransformComponent, SpriteComponent> (fun _ transformComp spriteComp ->
                    spriteComp.SetPosition transformComp.Position
                )
            )

            Behavior.update (fun _ em ea ->
                em.ForEach<SpriteRendererComponent> (fun _ rendererComp ->
                    rendererComp.Extra.Positions.Set (rendererComp.Positions, rendererComp.SpriteCount)
                    rendererComp.SpriteCount <- 0
                )
            )
        ]

