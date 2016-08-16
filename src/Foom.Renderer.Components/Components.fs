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

type MeshComponent (vertices: Vector3 [], uv) =
        
    let mutable min = Vector3.Zero
    let mutable max = Vector3.Zero

    do
        let firstV = vertices.[0]

        let mutable minX = firstV.X
        let mutable minY = firstV.Y
        let mutable minZ = firstV.Z
        let mutable maxX = firstV.X
        let mutable maxY = firstV.Y
        let mutable maxZ = firstV.Z

        vertices
        |> Array.iter (fun v ->
            if v.X < minX then
                minX <- v.X
            elif v.X > maxX then
               maxX <- v.X

            if v.Y < minY then
                minY <- v.Y
            elif v.Y > maxY then
               maxY <- v.Y

            if v.Z < minZ then
                minZ <- v.Z
            elif v.Z > maxZ then
               maxZ <- v.Z
        )

        min <- Vector3 (minX, minY, minZ)
        max <- Vector3 (maxX, maxY, maxZ)

    member val State = MeshState.ReadyToLoad (vertices, uv) with get, set

    member __.Min = min

    member __.Max = max

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

