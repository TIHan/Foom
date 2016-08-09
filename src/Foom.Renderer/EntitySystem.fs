module Foom.Renderer.EntitySystem

open System
open System.Numerics
open System.IO
open System.Drawing

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

type MaterialComponent (vertexShaderFileName: string, fragmentShaderFileName: string, textureFileName: string, color: Color) =

    member val TextureState = TextureState.ReadyToLoad textureFileName with get, set

    member val ShaderProgramState = ShaderProgramState.ReadyToLoad (vertexShaderFileName, fragmentShaderFileName) with get, set

    member val Color = color

    interface IEntityComponent

////////

let render (mvp: Matrix4x4) (entityManager: EntityManager) =
    entityManager.ForEach<MeshComponent, MaterialComponent> (fun ent meshComp materialComp ->
        match meshComp.State, materialComp.TextureState, materialComp.ShaderProgramState with
        | MeshState.Loaded bufferId, TextureState.Loaded textureId, ShaderProgramState.Loaded programId ->
            ()
        | _ -> ()
    )

let create () =
    let app = Renderer.init ()

    let meshComponentAddedQueue = createEventQueue<Events.ComponentAdded<MeshComponent>> ()
    let materialComponentAddedQueue = createEventQueue<Events.ComponentAdded<MaterialComponent>> ()

    system "Renderer" [ meshComponentAddedQueue.Hook; materialComponentAddedQueue.Hook ]

        (fun entityManager eventManager (deltaTime: float32) ->
   
            meshComponentAddedQueue.Process (fun componentAdded ->
                entityManager.TryGet<MeshComponent> (componentAdded.Entity)
                |> Option.iter (fun meshComp ->
                    match meshComp.State with
                    | MeshState.ReadyToLoad vertices ->
                        let vbo = Renderer.makeVbo ()
                        Renderer.bufferVboVector3 vertices (sizeof<Vector3> * vertices.Length) vbo
                        meshComp.State <- MeshState.Loaded (vbo)
                    | _ -> ()
                )
            )


            Renderer.clear ()

            let projection = Matrix4x4.CreatePerspectiveFieldOfView (1.f, (16.f / 9.f), 1.f, System.Single.MaxValue) |> Matrix4x4.Transpose
            let model = Matrix4x4.CreateTranslation (Vector3.Zero) |> Matrix4x4.Transpose
            let mvp = (projection * model) |> Matrix4x4.Transpose

            entityManager.TryFind<CameraComponent> (fun _ _ -> true)
            |> Option.iter (fun (ent, cameraComp) ->

                entityManager.TryGet<TransformComponent> (ent)
                |> Option.iter (fun transformComp ->
                    let mutable invertedTransform = transformComp.Transform
                    Matrix4x4.Invert(transformComp.Transform, &invertedTransform) |> ignore

                    let mvp = (projection * invertedTransform * model) |> Matrix4x4.Transpose

                    Renderer.enableDepth ()

                    render mvp entityManager

                    Renderer.disableDepth ()
                )
            )

            Renderer.draw app
        )