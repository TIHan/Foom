namespace Foom.Renderer

open System
open System.Numerics
open System.Runtime.Serialization

open Foom.Ecs
open Foom.Geometry

[<Sealed>]
type CameraComponent (projection : Matrix4x4) =
    inherit Component ()

    let mutable angle = Vector3 (0.f, 0.f, 0.f)

    [<IgnoreDataMember>]
    member val internal RenderCamera : RenderCamera option = None with get, set

    member val HeightOffset : single = 0.f with get, set

    member val HeightOffsetLerp : single = 0.f with get, set

    member val Projection = projection with get, set

    member this.Angle
        with get () = angle
        and set value = angle <- value

    member val AngleLerp = angle with get, set

    member this.AngleX 
        with get () = angle.X
        and set value = angle.X <- value

    member this.AngleY 
        with get () = angle.Y
        and set value = angle.Y <- value

    member this.AngleZ 
        with get () = angle.Z
        and set value = angle.Z <- value
