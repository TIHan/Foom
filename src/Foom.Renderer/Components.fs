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

type Mesh =
    {
        PositionBufferId: int
        PositionBufferLength: int

        UvBufferId: int
        UvBufferLength: int
    }

[<RequireQualifiedAccess>]
type MeshState =
    | ReadyToLoad of vertices: Vector3 [] * uv: Vector2 []
    | Loaded of Mesh

type MeshComponent (vertices: Vector3 [], uv) =

    member val State = MeshState.ReadyToLoad (vertices, uv) with get, set

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
