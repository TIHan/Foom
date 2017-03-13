module Foom.Client.Sprite

open System
open System.Drawing
open System.Numerics
open System.Collections.Generic

open Foom.Common.Components
open Foom.Ecs
open Foom.Renderer
open Foom.Collections
open Foom.Renderer.RendererSystem
open Foom.Game.Assets

[<Sealed>]
type Sprite (positions, lightLevels, uvOffsets) =
    inherit GpuResource ()

    member val Positions = Buffer.createVector3 positions

    member val LightLevels = Buffer.createVector4 lightLevels

    member val UvOffsets = Buffer.createVector4 uvOffsets
   

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

let vertices =
    [|
        Vector3 (-1.f, 0.f, 0.f)
        Vector3 (1.f, 0.f, 0.f)
        Vector3 (1.f, 0.f, 1.f)
        Vector3 (1.f, 0.f, 1.f)
        Vector3 (-1.f, 0.f, 1.f)
        Vector3 (-1.f, 0.f, 0.f)
    |]

let uv =
    [|
        Vector2 (0.f, 0.f * -1.f)
        Vector2 (1.f, 0.f * -1.f)
        Vector2 (1.f, 1.f * -1.f)
        Vector2 (1.f, 1.f * -1.f)
        Vector2 (0.f, 1.f * -1.f)
        Vector2 (0.f, 0.f * -1.f)
    |]

type SpriteRendererComponent (pipelineName, texture, lightLevel) =
    inherit RenderComponent<Sprite> (pipelineName, texture, Mesh (vertices, uv, createSpriteColor lightLevel), Sprite ([||], [||], [||]))

    member val SpriteCount = 0 with get, set

    member val Positions : Vector3 [] = Array.zeroCreate 100000

    member val LightLevels : Vector4 [] = Array.zeroCreate 100000

    member val UvOffsets : Vector4 [] = Array.zeroCreate 10000

[<Sealed>]
type SpriteComponent (pipelineName: string, texture: Texture, lightLevel: int) =
    inherit Component ()

    member val PipelineName = pipelineName

    member val Texture = texture

    member val Frame = 0 with get, set

    member val LightLevel = lightLevel with get, set

    member val RendererComponent : SpriteRendererComponent = Unchecked.defaultof<SpriteRendererComponent> with get, set

let handleSprite (am: AssetManager) =
    let lookup = Dictionary<string * Texture, Entity * SpriteRendererComponent> ()

    Behavior.merge
        [

            Behavior.handleComponentAdded (fun ent (comp: SpriteComponent) _ em ->
                am.LoadTexture (comp.Texture)

                let _, rendererComp = 
                    let key = (comp.PipelineName, comp.Texture)
                    match lookup.TryGetValue (key) with
                    | true, x -> x
                    | _ ->
                        let rendererComp = new SpriteRendererComponent(comp.PipelineName, comp.Texture, 255)

                        let rendererEnt = em.Spawn ()
                        em.Add (rendererEnt, rendererComp)

                        lookup.[key] <- (rendererEnt, rendererComp)

                        rendererEnt, rendererComp
                    
                comp.RendererComponent <- rendererComp
            )

            Behavior.update (fun _ em ea ->
                em.ForEach<TransformComponent, SpriteComponent> (fun _ transformComp spriteComp ->
                    let rendererComp = spriteComp.RendererComponent
                    if rendererComp.SpriteCount < rendererComp.Positions.Length then
                        let c = single spriteComp.LightLevel / 255.f

                        rendererComp.Positions.[rendererComp.SpriteCount] <- transformComp.Position
                        rendererComp.LightLevels.[rendererComp.SpriteCount] <- Vector4 (c, c, c, 1.f)

                        let frames = rendererComp.Texture.Frames
                        let frame = spriteComp.Frame
                        if frame < frames.Length then                  
                            rendererComp.UvOffsets.[rendererComp.SpriteCount] <- frames.[frame]

                        rendererComp.SpriteCount <- rendererComp.SpriteCount + 1
                )
            )

            Behavior.update (fun _ em ea ->
                em.ForEach<SpriteRendererComponent> (fun _ rendererComp ->
                    rendererComp.Extra.Positions.Set (rendererComp.Positions, rendererComp.SpriteCount)
                    rendererComp.Extra.LightLevels.Set (rendererComp.LightLevels, rendererComp.SpriteCount)
                    rendererComp.SpriteCount <- 0
                )
            )
        ]

