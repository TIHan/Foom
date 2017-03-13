module Foom.Client.Sprite

open System.Numerics

open Foom.Ecs
open Foom.Renderer

open Foom.Game.Assets

[<Sealed; Class>]
type Sprite =
    inherit GpuResource

    member Positions : Vector3Buffer

    member LightLevels : Vector4Buffer

    member UvOffsets : Vector4Buffer

[<Sealed; Class>]
type SpriteComponent =
    inherit Component

    new : pipelineName: string * texture: Texture * lightLevel: int -> SpriteComponent

    member PipelineName : string

    member Texture : Texture

    member LightLevel : int with get, set

val handleSprite : AssetManager -> Behavior<_>