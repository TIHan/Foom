namespace Foom.Common.Components

open System
open System.Numerics

open Foom.Ecs

[<Sealed>]
type TransformComponent (value: Matrix4x4) =

    let mutable transform = value

    member __.Transform
        with get () = transform

        and set value =
            transform <- value

    member val TransformLerp : Matrix4x4 = transform with get, set

    member this.Position 
        with get () =
            transform.Translation
         
        and set value =
            transform.Translation <- value

    member this.ApplyYawPitchRoll (yaw: float32, pitch: float32, roll: float32) =
        let yaw = yaw * (float32 Math.PI / 180.f)
        let pitch = pitch * (float32 Math.PI / 180.f)
        let roll = roll * (float32 Math.PI / 180.f)
        let m = Matrix4x4.CreateFromYawPitchRoll (yaw, pitch, roll)
        transform <- m * transform

    member this.RotateX (degrees: float32) =
        let radians = degrees * (float32 Math.PI / 180.f)
        transform <- Matrix4x4.CreateRotationX (radians) * transform

    member this.RotateY (degrees: float32) =
        let radians = degrees * (float32 Math.PI / 180.f)
        transform <- Matrix4x4.CreateRotationY (radians) * transform

    member this.RotateZ (degrees: float32) =
        let radians = degrees * (float32 Math.PI / 180.f)
        transform <- Matrix4x4.CreateRotationZ (radians) * transform

    member this.Translate v =
        this.Position <- this.Position + v

    member this.Rotation 
        with get () = Quaternion.CreateFromRotationMatrix (transform)

        and set value = 
            let mutable m = Matrix4x4.CreateFromQuaternion (value)
            m.Translation <- transform.Translation
            transform <- m

    interface IComponent
    