namespace Foom.Game.Sprite

open System
open System.Numerics
open System.Collections.Generic

open Foom.Ecs
open Foom.Renderer
open Foom.Collections
open Foom.Renderer
open Foom.Renderer.RendererSystem
open Foom.Game.Assets
open Foom.Game.Core
open System.Runtime.Serialization

[<AutoOpen>]
module SpriteHelpers =

    let createSpriteColor lightLevel =
        let color = Array.init 6 (fun _ -> Vector4 (255.f, lightLevel, lightLevel, lightLevel))
        color
        |> Array.map (fun c ->
            Vector4 (
                c.X / 255.f,
                c.Y / 255.f,
                c.Z / 255.f,
                c.W / 255.f)
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

type SpriteBatchInput (shaderInput) =
    inherit MeshInput (shaderInput)

    member val Center = shaderInput.CreateVertexAttributeVar<Vector3Buffer> ("in_center")

    member val Positions = shaderInput.CreateInstanceAttributeVar<Vector3Buffer> ("instance_position")

    member val LightLevels = shaderInput.CreateInstanceAttributeVar<Vector4Buffer> ("instance_lightLevel")

    member val UvOffsets = shaderInput.CreateInstanceAttributeVar<Vector4Buffer> ("instance_uvOffset")

type SpriteBatch (lightLevel, material : BaseMaterial) =
    inherit Mesh<SpriteBatchInput> (vertices, uv, createSpriteColor lightLevel)

    member val PositionBuffer = Buffer.createVector3 [||]

    member val Positions = Array.zeroCreate 1000000

    member val LightLevelBuffer = Buffer.createVector4 [||]

    member val LightLevels = Array.zeroCreate 1000000

    member val UvOffsetBuffer = Buffer.createVector4 [||]

    member val UvOffsets = Array.zeroCreate 1000000

    member val SpriteCount = 0 with get, set

    member val Material = material

    override this.SetShaderInput input =
        base.SetShaderInput input

        input.Positions.Set this.PositionBuffer
        input.LightLevels.Set this.LightLevelBuffer
        input.UvOffsets.Set this.UvOffsetBuffer

//type SpriteBatchRendererComponent (layer, material, lightLevel) =
//    inherit MeshRendererComponent<SpriteBatchInput, SpriteBatch> (layer, material, SpriteBatch lightLevel)

//    member val SpriteCount = 0 with get, set

//    member val Positions : Vector3 [] = Array.zeroCreate 1000000

//    member val LightLevels : Vector4 [] = Array.zeroCreate 1000000

//    member val UvOffsets : Vector4 [] = Array.zeroCreate 100000

[<Sealed>]
type SpriteComponent (layer : int, textureKind : TextureKind, lightLevel: int) =
    inherit Component ()

    member val Layer = layer

    member val Frame = 0 with get, set

    member val LightLevel = lightLevel with get, set

    member val TextureKind = textureKind

    [<IgnoreDataMember>]
    member val Batch : SpriteBatch = Unchecked.defaultof<SpriteBatch> with get, set

module Sprite =

    let shader = CreateShader SpriteBatchInput 0 (CreateShaderPass (fun _ -> []) "Sprite")

    let update (am: AssetManager) (renderer : Renderer) : Behavior<float32 * float32> =
        let lookup = Dictionary<int * TextureKind, SpriteBatch> ()

        let rendererSpawnQueue = Queue ()

        Behavior.Merge
            [
                Behavior.ComponentAdded (fun _ ent (comp: SpriteComponent) ->
                    rendererSpawnQueue.Enqueue (comp)
                )

                Behavior.Update (fun _ em _ ->
                    while rendererSpawnQueue.Count > 0 do
                        let comp = rendererSpawnQueue.Dequeue ()
                        let batch = 
                            let key = (comp.Layer, comp.TextureKind)
                            match lookup.TryGetValue (key) with
                            | true, x -> x
                            | _ ->
                                let material = MaterialDescription<SpriteBatchInput> (shader, comp.TextureKind) |> am.GetMaterial
                                let batch = SpriteBatch (255.f, material)

                                renderer.AddMesh (comp.Layer, material.Shader :?> Shader<SpriteBatchInput>, material.Texture.Buffer, batch)

                                lookup.[key] <- batch

                                batch
                        
                        comp.Batch <- batch
                )

                Behavior.Update (fun _ em ea ->
                    em.ForEach<TransformComponent, SpriteComponent> (fun _ transformComp spriteComp ->
                        let rendererComp = spriteComp.Batch
                        if rendererComp.SpriteCount < rendererComp.Positions.Length then
                            let c = single spriteComp.LightLevel / 255.f

                            rendererComp.Positions.[rendererComp.SpriteCount] <- transformComp.Position
                            rendererComp.LightLevels.[rendererComp.SpriteCount] <- Vector4 (c, c, c, 1.f)

                            let material = rendererComp.Material
                            let frames = material.Texture.Frames
                            let frame = spriteComp.Frame
                            let frame = 
                                if frame >= frames.Length then 0
                                else frame
                                                 
                            rendererComp.UvOffsets.[rendererComp.SpriteCount] <- frames.[frame]

                            rendererComp.SpriteCount <- rendererComp.SpriteCount + 1
                    )
                )

                Behavior.Update (fun _ em ea ->
                    lookup
                    |> Seq.iter (fun pair ->
                        let rendererComp = pair.Value
                        rendererComp.PositionBuffer.Set (rendererComp.Positions, rendererComp.SpriteCount)
                        rendererComp.LightLevelBuffer.Set (rendererComp.LightLevels, rendererComp.SpriteCount)
                        rendererComp.UvOffsetBuffer.Set (rendererComp.UvOffsets, rendererComp.SpriteCount)
                        rendererComp.SpriteCount <- 0
                    )
                )
            ]

