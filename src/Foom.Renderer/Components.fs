namespace Foom.Renderer

open System
open System.Drawing
open System.Numerics

open Foom.Ecs

type Mesh =
    {
        Position: Vector3ArrayBuffer
        Uv: Vector2ArrayBuffer
    }

type WireframeComponent =
    {
        Position: Vector3ArrayBuffer
    }

    interface IComponent

type MeshComponent (position, uv) =

    member val Mesh = { Position = Vector3ArrayBuffer (position); Uv = Vector2ArrayBuffer (uv) }

    interface IComponent

[<RequireQualifiedAccess>]
type ShaderProgramState =
    | ReadyToLoad of vsFileName: string * fsFileName: string
    | Loaded of programId: int

type MaterialComponent (vertexShaderFileName: string, fragmentShaderFileName: string, texture: Texture2DBuffer option, color: Color) =

    member val Texture = texture with get

    member val ShaderProgramState = ShaderProgramState.ReadyToLoad (vertexShaderFileName, fragmentShaderFileName) with get, set

    member val Color = color

    interface IComponent
