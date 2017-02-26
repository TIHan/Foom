module Foom.Client.Sprite

open System.Numerics

open Foom.Ecs
open Foom.Renderer

[<Sealed; Class>]
type Sprite =
    inherit GpuResource

    member Center : Vector3Buffer

    member Positions : Vector3Buffer

[<Sealed; Class>]
type SpriteComponent =
    inherit Component

    new : subRenderer: string * texture: string -> SpriteComponent

    member SubRenderer : string

    member Texture : string