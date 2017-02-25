module Foom.Client.Sprite

open System.Numerics

open Foom.Ecs
open Foom.Renderer

type Sprite =
    inherit GpuResource

    new : Vector3 [] -> Sprite