module Foom.Renderer.RendererSystem

open System
open System.Numerics
open System.IO
open System.Drawing
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Common.Components

type MeshComponent (mesh) =

    member val Mesh = mesh

    interface IComponent

type MaterialComponent (material) =

    member val Material = material

    interface IComponent

let renderer = FRenderer.Create ()

let componentAddedQueue f =
    Behavior.handleEvent (fun (componentAdded: Events.ComponentAdded<'T>) (_, deltaTime: float32) entityManager ->
        entityManager.TryGet<'T> (componentAdded.Entity)
        |> Option.iter (fun comp ->
            f componentAdded.Entity comp entityManager
        )
    )

let shaderCache = Dictionary<string * string, int> ()
let materialQueue =
    componentAddedQueue (fun ent (materialComp: MaterialComponent) em ->

        match em.TryGet<TransformComponent> ent with
        | Some transformComp ->

            match em.TryGet<MeshComponent> ent with
            | Some meshComp ->

                let material = materialComp.Material

                let programId =

                    match material.ShaderProgramId with
                    | None ->
                        let vertex = material.VertexShaderFileName
                        let fragment = material.FragmentShaderFileName

                        match shaderCache.TryGetValue ((vertex, fragment)) with
                        | true, programId ->
                            material.ShaderProgramId <- Some programId
                            programId

                        | _ ->
                            let mutable vertexFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (vertex))
                            let mutable fragmentFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (fragment))

                            let programId = Renderer.loadShaders vertexFile fragmentFile
                            material.ShaderProgramId <- Some programId
                            shaderCache.Add ((vertex, fragment), programId)
                            programId

                    | Some programId -> programId

             
                let textureId, texture =
                    match material.Texture with
                    | Some texture ->
                        texture.TryBufferData () |> ignore
                        texture.Id, Some texture
                    | _ ->
                        0, None

                let mesh = meshComp.Mesh
                let getTransform = fun () -> transformComp.Transform
                state.Add (ent, programId, textureId, texture, getTransform, mesh.Position, mesh.Uv, material.Color)

            | _ -> ()
        | _ -> ()

    )

let create (app: Application) : ESystem<float32 * float32> =

    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    ESystem.create "Renderer"
        [
            materialQueue

            Behavior.update (fun ((time, deltaTime): float32 * float32) entityManager eventManager ->

                renderer.Clear ()

                entityManager.TryFind<CameraComponent> (fun _ _ -> true)
                |> Option.iter (fun (ent, cameraComp) ->

                    entityManager.TryGet<TransformComponent> (ent)
                    |> Option.iter (fun transformComp ->

                        let heightOffset = Mathf.lerp cameraComp.HeightOffsetLerp cameraComp.HeightOffset deltaTime

                        let projection = cameraComp.Projection |> Matrix4x4.Transpose
                        let mutable transform = Matrix4x4.Lerp (transformComp.TransformLerp, transformComp.Transform, deltaTime)

                        let mutable v = transform.Translation


                        v.Z <- zEasing.Update (transformComp.Position.Z, time)

                        transform.Translation <- v + Vector3(0.f,0.f,heightOffset)

                        let mutable invertedTransform = Matrix4x4.Identity

                        Matrix4x4.Invert(transform, &invertedTransform) |> ignore

                        let invertedTransform = invertedTransform |> Matrix4x4.Transpose

                        renderer.Draw projection invertedTransform transformComp.Transform
                    )
                )

                Renderer.draw app

            )

        ]
