module Foom.Client.Sprite

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

open Foom.Common.Components
open Foom.Ecs
open Foom.Renderer
open Foom.Renderer.RendererSystem

type Sprite (center) =
    inherit GpuResource ()

    member val Center = Buffer.createVector3 center

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

type SpriteRenderComponent (subRenderer, texture, lightLevel) =
    inherit RenderComponent<Sprite> (subRenderer, texture, Mesh ([||], uv, createSpriteColor lightLevel), Sprite ([||]))

[<Sealed>]
type SpriteComponent (subRenderer: string, texture: string) =
    inherit Component ()
    
    member val IndexRef = ref -1 with get, set

    member val SubRenderer = subRenderer

    member val Texture = texture

    member this.SetPosition : Vector3 -> unit = fun pos -> ()

let handleSprite () =
    let lookup = Dictionary<string * string, SpriteRenderComponent> ()
    let textureLookup = Dictionary<string, int * int> ()
    Behavior.merge
        [
            Behavior.handleComponentAdded (fun ent (comp: SpriteRenderComponent) _ em ->
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
            Behavior.handleEvent (fun (evt: Events.ComponentAdded<SpriteRenderComponent>) _ em ->
                match em.TryGet<SpriteRenderComponent> (evt.Entity) with
                | Some comp -> 
                    ()
                    // check to see if SpriteRenderComponent exists
                    // if not, then spawn one
                    // add sprite's vector3 position and get index for it
                | _ -> ()
            )

            Behavior.update (fun _ em ea ->
                em.ForEach<TransformComponent, SpriteComponent> (fun _ transformComp spriteComp ->
                    spriteComp.SetPosition transformComp.Position
                )
            )
        ]

