namespace Foom.Shared.Components

open System
open System.Numerics

open Foom.Ecs

[<Sealed>]
type Transform (value: Matrix4x4) =

    member val Value : Matrix4x4 = value with get, set

    interface IEntityComponent

[<Sealed>]
type Camera () = class end


