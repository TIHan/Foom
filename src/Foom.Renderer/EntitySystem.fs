module Foom.Renderer.EntitySystem

open System
open System.Numerics
open System.IO

open Foom.Ecs
open Foom.Common.Components

[<RequireQualifiedAccess>]
type MeshState =
    | ReadyToLoad of vertices: Vector3 []
    | Loaded of bufferId: int

type MeshComponent (vertices: Vector3 []) =

    member val State = MeshState.ReadyToLoad (vertices) with get, set

    interface IEntityComponent


[<RequireQualifiedAccess>]
type TextureState =
    | ReadyToLoad of fileName: string
    | Loaded of textureId: int

[<RequireQualifiedAccess>]
type ShaderProgramState =
    | ReadyToLoad of vsFileName: string * fsFileName: string
    | Loaded of programId: int

type MaterialComponent (vertexShaderFileName: string, fragmentShaderFileName: string, textureFileName: string) =

    member val TextureState = TextureState.ReadyToLoad textureFileName

    member val ShaderProgramState = ShaderProgramState.ReadyToLoad (vertexShaderFileName, fragmentShaderFileName)

    interface IEntityComponent

////////

let create () =
    let app = Renderer.init ()

    let vbos = ResizeArray<int>()

    let programs = ResizeArray<int> ()

    Systems.system "Renderer" (fun entityManager eventManager (deltaTime: float32) ->
        Renderer.clear ()

        let projection = Matrix4x4.CreatePerspectiveFieldOfView (1.f, (16.f / 9.f), 1.f, System.Single.MaxValue) |> Matrix4x4.Transpose
        let model = Matrix4x4.CreateTranslation (Vector3.Zero) |> Matrix4x4.Transpose
        let mvp = (projection * model) |> Matrix4x4.Transpose

        match entityManager.TryFind<CameraComponent> (fun _ _ -> true) with
        | Some (ent, cameraComp) ->
   
            match entityManager.TryGet<TransformComponent> (ent) with
            | Some transformComp ->
                let mutable invertedTransform = transformComp.Transform
                Matrix4x4.Invert(transformComp.Transform, &invertedTransform) |> ignore

                let mvp = (projection * invertedTransform * model) |> Matrix4x4.Transpose

                ()
                // let's do our rendering stuffz

            | _ -> ()

        | _ -> ()



        Renderer.draw app
    )