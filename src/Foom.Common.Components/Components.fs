namespace Foom.Common.Components

open System
open System.Numerics

open Foom.Ecs

[<Sealed>]
type TransformComponent (value: Matrix4x4) =

    let mutable transform = value

    member __.Transform : Matrix4x4 = transform

    member this.Position 
        with get () =
            transform.Translation
         
        and set value =
            transform.Translation <- value

    interface IEntityComponent

[<Sealed>]
type CameraComponent () =

    interface IEntityComponent


    