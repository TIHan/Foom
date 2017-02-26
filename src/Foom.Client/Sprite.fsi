module Foom.Client.Sprite

open System.Numerics

open Foom.Ecs
open Foom.Renderer

[<Sealed; Class>]
type Sprite =
    inherit GpuResource

    member Center : Vector3Buffer

    member Positions : Vector3Buffer

    member LightLevels : Vector4Buffer

[<Sealed; Class>]
type SpriteComponent =
    inherit Component

    new : subRenderer: string * texture: string * lightLevel: int -> SpriteComponent

    member SubRenderer : string

    member Texture : string

    member LightLevel : int with get, set

val handleSprite : unit -> Behavior<_>