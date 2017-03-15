namespace Foom.Game.Level

open System.Numerics

type SectorGeometry =
    {
        vertices: Vector3 []

        mutable height: float32
        textureName: string option
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module SectorGeometry =

    let create vertices height textureName =
        {
            vertices = vertices
            height = height
            textureName = textureName
        }

    let changeHeight height flatPart =
        flatPart.height <- height
        for i = 0 to flatPart.vertices.Length - 1 do
            flatPart.vertices.[i].Z <- height

    let createUV width height (flatPart: SectorGeometry) =
        let vertices = flatPart.vertices
        let width = single width
        let height = single height * -1.f
        let uv = Array.zeroCreate (vertices.Length)

        let mutable i = 0
        while i < vertices.Length do

            let v1 = vertices.[i]
            let v2 = vertices.[i + 1]
            let v3 = vertices.[i + 2]

            uv.[i] <- Vector2 (v1.X / width, v1.Y / height)
            uv.[i + 1] <- Vector2 (v2.X / width, v2.Y / height)
            uv.[i + 2] <- Vector2 (v3.X / width, v3.Y / height)

            i <- i + 3

        uv

type SectorGeometry with

    member this.Vertices = this.vertices

    member this.Height = this.height

    member this.TextureName = this.textureName
