namespace Foom.Level

open System
open System.Numerics
open System.Collections.Generic

open Foom.Geometry

type TextureAlignment =
    | UpperUnpegged of offsetY: int
    | LowerUnpegged

type WallPartSide =
    {
        TextureOffsetX: int
        TextureOffsetY: int
        TextureName: string option
        Vertices: Vector3 []
        TextureAlignment: TextureAlignment
    }

type WallPart =
    {
        FrontSide: WallPartSide option
        BackSide: WallPartSide option
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module WallPart =

    let updateUV (uv: Vector2 []) width height (side: WallPartSide) =
        let vertices = side.Vertices

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

            let one = 0.f + single side.TextureOffsetX
            let two = (v2 - v1).Length ()

            let x, y, z1, z3 =

                // lower unpeg
                match side.TextureAlignment with
                | LowerUnpegged ->
                    let ofsY = single side.TextureOffsetY / height * -1.f
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
                    let ofsY = single side.TextureOffsetY / height * -1.f
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

    let updateFrontUV uv width height (wallPart: WallPart) =
        if wallPart.FrontSide.IsNone then ()
        else

        updateUV uv width height wallPart.FrontSide.Value


    let createFrontUV width height (wallPart: WallPart) =
        if wallPart.FrontSide.IsNone then [||]
        else

        let side = wallPart.FrontSide.Value

        let vertices = side.Vertices
        let uv = Array.zeroCreate vertices.Length

        updateUV uv width height wallPart.FrontSide.Value

        uv

    let updateBackUV uv width height (wallPart: WallPart) =
        if wallPart.BackSide.IsNone then ()
        else

        updateUV uv width height wallPart.BackSide.Value


    let createBackUV width height (wallPart: WallPart) =
        if wallPart.BackSide.IsNone then [||]
        else

        let side = wallPart.BackSide.Value

        let vertices = side.Vertices
        let uv = Array.zeroCreate vertices.Length

        updateUV uv width height wallPart.BackSide.Value

        uv
