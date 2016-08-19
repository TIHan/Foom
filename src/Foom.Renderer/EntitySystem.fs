module Foom.Renderer.EntitySystem

open System
open System.Numerics
open System.IO
open System.Drawing
open System.Collections.Generic

open Foom.Ecs
open Foom.Common.Components
open Foom.Renderer.Components

////////

let render (projection: Matrix4x4) (view: Matrix4x4) (cameraModel: Matrix4x4) (entityManager: EntityManager) =
    let renderQueue = Queue<Mesh * int * int * Matrix4x4 * MaterialComponent * MeshComponent> ()
    let transparentQueue = ResizeArray<Mesh * int * int * Matrix4x4 * MaterialComponent * MeshComponent> ()

    entityManager.ForEach<MeshComponent, MaterialComponent, TransformComponent> (fun ent meshComp materialComp transformComp ->
        let model = transformComp.Transform

        let mvp = (projection * view) |> Matrix4x4.Transpose

        match meshComp.State, materialComp.TextureState, materialComp.ShaderProgramState with
        | MeshState.Loaded mesh, TextureState.Loaded textureId, ShaderProgramState.Loaded programId ->
                renderQueue.Enqueue ((mesh, textureId, programId, mvp, materialComp, meshComp))
        | _ -> ()
    )

    Renderer.enableDepth ()

    while (renderQueue.Count > 0) do
        let mesh, textureId, programId, mvp, materialComp, _ = renderQueue.Dequeue ()

        Renderer.useProgram programId

        let uniformColor = Renderer.getUniformColor programId
        let uniformProjection = Renderer.getUniformProjection programId

        Renderer.setUniformProjection uniformProjection mvp
        Renderer.setTexture programId textureId

        Renderer.bindVbo mesh.PositionBufferId
        Renderer.bindPosition programId

        Renderer.bindVbo mesh.UvBufferId
        Renderer.bindUv programId

        Renderer.bindTexture textureId

        Renderer.setUniformColor uniformColor (Color.FromArgb (255, int materialComp.Color.R, int materialComp.Color.G, int materialComp.Color.B) |> RenderColor.OfColor)
        Renderer.drawTriangles 0 mesh.PositionBufferLength

    Renderer.disableDepth ()

    entityManager.ForEach<MaterialComponent, WireframeComponent> (fun ent materialComp wireframeComp ->

        let mvp = (projection * view) |> Matrix4x4.Transpose

        match wireframeComp.State, materialComp.ShaderProgramState with
        | WireframeState.Loaded mesh, ShaderProgramState.Loaded programId ->
            Renderer.useProgram programId

            let uniformColor = Renderer.getUniformColor programId
            let uniformProjection = Renderer.getUniformProjection programId

            Renderer.setUniformProjection uniformProjection mvp

            Renderer.bindVbo mesh.PositionBufferId
            Renderer.bindPosition programId

            Renderer.setUniformColor uniformColor (Color.FromArgb (255, int materialComp.Color.R, int materialComp.Color.G, int materialComp.Color.B) |> RenderColor.OfColor)
            Renderer.drawArrays 0 mesh.PositionBufferLength
        | _ -> ()
    )

let wireframeQueue =
    eventQueue (fun entityManager eventManager ->

        fun (deltaTime: float32) (componentAdded: Events.ComponentAdded<WireframeComponent>) ->

            entityManager.TryGet<WireframeComponent> (componentAdded.Entity)
            |> Option.iter (fun meshComp ->

                match meshComp.State with
                | WireframeState.ReadyToLoad (vertices) ->
                    let vbo = Renderer.makeVbo ()
                    Renderer.bufferVboVector3 vertices (sizeof<Vector3> * vertices.Length) vbo

                    meshComp.State <- 
                        WireframeState.Loaded
                            {
                                PositionBufferId = vbo
                                PositionBufferLength = vertices.Length
                            }
                | _ -> ()

            )
     
    )

let meshQueue =
    eventQueue (fun entityManager eventManager ->

        fun (deltaTime: float32) (componentAdded: Events.ComponentAdded<MeshComponent>) ->

            entityManager.TryGet<MeshComponent> (componentAdded.Entity)
            |> Option.iter (fun meshComp ->

                match meshComp.State with
                | MeshState.ReadyToLoad (vertices, uv) ->
                    let vbo = Renderer.makeVbo ()
                    Renderer.bufferVboVector3 vertices (sizeof<Vector3> * vertices.Length) vbo

                    let vbo2 = Renderer.makeVbo ()
                    Renderer.bufferVbo uv (sizeof<Vector2> * uv.Length) vbo2

                    meshComp.State <- 
                        MeshState.Loaded
                            {
                                PositionBufferId = vbo
                                PositionBufferLength = vertices.Length

                                UvBufferId = vbo2
                                UvBufferLength = uv.Length
                            }
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
                        try
                            use ptr = new Gdk.Pixbuf (fileName)
                            let textureId = Renderer.createTexture ptr.Width ptr.Height (ptr.Pixels)

                            materialComp.TextureState <- TextureState.Loaded textureId
                        with | ex ->
                            printfn "%A" ex.Message
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

let create (app: Application) =

    EntitySystem.create "Renderer"
        [
            wireframeQueue
            meshQueue
            materialQueue

            update (fun entityManager eventManager deltaTime ->

                Renderer.clear ()

                let projection = Matrix4x4.CreatePerspectiveFieldOfView (56.25f * 0.0174533f, ((16.f + 16.f * 0.25f) / 9.f), 32.f, System.Single.MaxValue) |> Matrix4x4.Transpose

                entityManager.TryFind<CameraComponent> (fun _ _ -> true)
                |> Option.iter (fun (ent, cameraComp) ->

                    entityManager.TryGet<TransformComponent> (ent)
                    |> Option.iter (fun transformComp ->

                        let transform = Matrix4x4.Lerp (transformComp.TransformLerp, transformComp.Transform, deltaTime)

                        let mutable invertedTransform = Matrix4x4.Identity

                        Matrix4x4.Invert(transform, &invertedTransform) |> ignore

                        let invertedTransform = invertedTransform |> Matrix4x4.Transpose

                        render projection invertedTransform transformComp.Transform entityManager
                    )
                )

                Renderer.draw app

            )

        ]
