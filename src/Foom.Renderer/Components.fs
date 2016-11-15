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

type Material =
    {
        VertexShaderFileName: string
        FragmentShaderFileName: string
        Texture: Texture2DBuffer option
        Color: Color
        mutable ShaderProgramId: int option
    }

type WireframeComponent =
    {
        Position: Vector3ArrayBuffer
    }

    interface IComponent

type MeshComponent (position, uv) =

    member val Mesh = { Position = Vector3ArrayBuffer (position); Uv = Vector2ArrayBuffer (uv) }

    interface IComponent

type MaterialComponent (vertexShaderFileName: string, fragmentShaderFileName: string, texture: Texture2DBuffer option, color: Color) =

    member val Material =
        {
            VertexShaderFileName = vertexShaderFileName
            FragmentShaderFileName = fragmentShaderFileName
            Texture = texture
            Color = color
            ShaderProgramId = None
        }

    interface IComponent
