namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics

open Foom.Ecs

type WireframeComponent =
    {
        Position: Vector3ArrayBuffer
    }

    interface IComponent

type MeshComponent =
    {
        Position: Vector3ArrayBuffer
        Uv: Vector2ArrayBuffer
    }

    interface IComponent

[<RequireQualifiedAccess>]
type TextureState =
    | ReadyToLoad of fileName: string
    | Loaded of textureId: int

[<RequireQualifiedAccess>]
type ShaderProgramState =
    | ReadyToLoad of vsFileName: string * fsFileName: string
    | Loaded of programId: int

type MaterialComponent (vertexShaderFileName: string, fragmentShaderFileName: string, textureFileName: string, color: Color) =

    member val TextureState = TextureState.ReadyToLoad textureFileName with get, set

    member val ShaderProgramState = ShaderProgramState.ReadyToLoad (vertexShaderFileName, fragmentShaderFileName) with get, set

    member val Color = color

    interface IComponent
