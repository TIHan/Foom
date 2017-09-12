namespace Foom.Game.Sprite

open System
open System.Numerics

open Foom.Ecs
open Foom.Renderer

open Foom.Game.Assets

[<Sealed; Class>]
type SpriteComponent =
    inherit Component

    new : layer : int * textureKind : TextureKind * lightLevel: int -> SpriteComponent

    member Layer : int

    member TextureKind : TextureKind

    member Frame : int with get, set

    member LightLevel : int with get, set

module Sprite =

    val update : AssetManager -> Renderer -> Behavior<float32 * float32>
