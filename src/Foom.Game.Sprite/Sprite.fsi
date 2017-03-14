namespace Foom.Game.Sprite

open System
open System.Numerics

open Foom.Ecs
open Foom.Renderer

open Foom.Game.Assets

[<Sealed; Class>]
type SpriteComponent =
    inherit Component

    new : pipelineName: string * texture: Texture * lightLevel: int -> SpriteComponent

    member PipelineName : string

    member Texture : Texture

    member LightLevel : int with get, set

module Sprite =

    val pipeline : Pipeline<unit>

    val update : AssetManager -> Behavior<float32 * float32>

type AnimatedSpriteComponent =
    inherit Component

    new : time: TimeSpan * int list -> AnimatedSpriteComponent