namespace Foom.Game.Sprite

open System
open System.Numerics

open Foom.Ecs
open Foom.Renderer

open Foom.Game.Assets

[<Sealed; Class>]
type SpriteComponent =
    inherit Component

    new : layer : int * texture: Texture * lightLevel: int -> SpriteComponent

    member Layer : int

    member Texture : Texture

    member Frame : int with get, set

    member LightLevel : int with get, set

module Sprite =

    val update : AssetManager -> Behavior<float32 * float32>
