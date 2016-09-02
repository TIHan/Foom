namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics

open Foom.Ecs

type Wireframe =
    {
        PositionBufferId: int
        PositionBufferLength: int
    }

[<RequireQualifiedAccess>]
type WireframeState =
    | ReadyToLoad of vertices: Vector3 []
    | Loaded of Wireframe

type WireframeComponent (vertices: Vector3 []) =

    member val State = WireframeState.ReadyToLoad (vertices) with get, set

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
