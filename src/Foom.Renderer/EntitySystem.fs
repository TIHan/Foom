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



let meshQueue =
    eventQueue (fun entityManager eventManager ->

        fun (deltaTime: float32) (componentAdded: Events.ComponentAdded<MeshComponent>) ->

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

let materialQueue =
    eventQueue (fun entityManager eventManager ->

        fun (deltaTime: float32) (componentAdded: Events.ComponentAdded<MaterialComponent>) ->

            entityManager.TryGet<MaterialComponent> (componentAdded.Entity)
            |> Option.iter (fun materialComp ->
                 
                    match materialComp.TextureState with
                    | TextureState.ReadyToLoad fileName ->
                        use ptr = new Gdk.Pixbuf (fileName)
                        let textureId = Renderer.createTexture 64 64 (ptr.Pixels)

                        materialComp.TextureState <- TextureState.Loaded textureId
                    | _ -> ()

                    match materialComp.ShaderProgramState with
                    | ShaderProgramState.ReadyToLoad (vertex, fragment) ->
                        let mutable vertexFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (vertex))
                        let mutable fragmentFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (fragment))

                        let programId = Renderer.loadShaders vertexFile fragmentFile
                        materialComp.ShaderProgramState <- ShaderProgramState.Loaded programId

                    | _ -> ()

            )
     
    )

let create () =
    let app = Renderer.init ()

    EntitySystem.create "Renderer"
        [
            meshQueue
            materialQueue

            update (fun entityManager eventManager deltaTime ->

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

        ]
