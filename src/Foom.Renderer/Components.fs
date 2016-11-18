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
