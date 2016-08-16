namespace Foom.Renderer.Components

open System.Numerics

open Foom.Ecs

type Color =
    {
        R: byte
        G: byte
        B: byte
        A: byte
    }

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

type MeshComponent (vertices, uv) =

    member val State = MeshState.ReadyToLoad (vertices, uv) with get, set

    interface IEntityComponent


[<RequireQualifiedAccess>]
type TextureState =
    | ReadyToLoad of fileName: string
    | Loaded of textureId: int

[<RequireQualifiedAccess>]
type ShaderProgramState =
    | ReadyToLoad of vsFileName: string * fsFileName: string
    | Loaded of programId: int

type MaterialComponent (vertexShaderFileName: string, fragmentShaderFileName: string, textureFileName: string, color: Color, isTransparent: bool) =

    member val TextureState = TextureState.ReadyToLoad textureFileName with get, set

    member val ShaderProgramState = ShaderProgramState.ReadyToLoad (vertexShaderFileName, fragmentShaderFileName) with get, set

    member val Color = color

    member val IsTransparent = isTransparent

    interface IEntityComponent

