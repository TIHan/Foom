namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry
open Foom.Wad.Level.Structures

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type WallPart =
    {
        TextureOffsetX: int
        TextureOffsetY: int
        TextureName: string option
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module WallPart =

    let updateUV (uv: Vector2 []) width height (wallPart: WallPart) =
        let vertices = wallPart.Vertices

        let mutable i = 0
        while (i < vertices.Length) do
            let p1 = vertices.[i]
            let p2 = vertices.[i + 1]
            let p3 = vertices.[i + 2]

            let width = single width
            let height = single height

            let v1 = Vector2 (p1.X, p1.Y)
            let v2 = Vector2 (p2.X, p2.Y)
            let v3 = Vector2 (p3.X, p3.Y)

            let one = 0.f + single wallPart.TextureOffsetX
            let two = (v2 - v1).Length ()

            let x, y, z1, z3 =

                // lower unpeg
                match wallPart.TextureAlignment with
                | LowerUnpegged ->
                    let ofsY = single wallPart.TextureOffsetY / height * -1.f
                    if p3.Z < p1.Z then
                        (one + two) / width, 
                        one / width, 
                        0.f - ofsY,
                        ((abs (p1.Z - p3.Z)) / height * -1.f) - ofsY
                    else
                        one / width, 
                        (one + two) / width, 
                        ((abs (p1.Z - p3.Z)) / height * -1.f) - ofsY,
                        0.f - ofsY

                // upper unpeg
                | UpperUnpegged offsetY ->
                    let z = single offsetY / height * -1.f
                    let ofsY = single wallPart.TextureOffsetY / height * -1.f
                    if p3.Z < p1.Z then
                        (one + two) / width, 
                        one / width, 
                        (1.f - ((abs (p1.Z - p3.Z)) / height * -1.f)) - z - ofsY,
                        1.f - z - ofsY
                    else
                        one / width, 
                        (one + two) / width, 
                        1.f - z - ofsY,
                        (1.f - ((abs (p1.Z - p3.Z)) / height * -1.f)) - z - ofsY

            

            uv.[i] <- Vector2 (x, z3)
            uv.[i + 1] <- Vector2(y, z3)
            uv.[i + 2] <- Vector2(y, z1)

            i <- i + 3

    let createUV width height (wallPart: WallPart) =
        let vertices = wallPart.Vertices
        let uv = Array.zeroCreate vertices.Length
        updateUV uv width height wallPart
        uv
