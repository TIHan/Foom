namespace Foom.Common.Components

open System
open System.Numerics

open Foom.Ecs

[<Sealed>]
type TransformComponent (value: Matrix4x4) =

    member val Transform : Matrix4x4 = value with get, set

    interface IEntityComponent

[<Sealed>]
type CameraComponent () =

    interface IEntityComponent


    