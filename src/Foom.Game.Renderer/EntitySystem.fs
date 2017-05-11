module Foom.Renderer.RendererSystem

open System
open System.Reflection
open System.Numerics
open System.IO
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Renderer

open Foom.Game.Assets
open Foom.Game.Core

[<AbstractClass>]
type BaseMeshRendererComponent (layer : int, materialDesc : IMaterialDescription, mesh : BaseMesh) =
    inherit Component ()

    member val Layer = layer

    member val MaterialDescription = materialDesc

    member val BaseMesh = mesh

    member val Material = None with get, set

[<AbstractClass>]
type MeshRendererComponent<'T, 'U when 'T :> MeshInput and 'U :> Mesh<'T>> (group, material : MaterialDescription<'T>, mesh : 'U) =
    inherit BaseMeshRendererComponent (group, material, mesh)

    member val Mesh = mesh

[<Sealed>]
type MeshRendererComponent (group, material : MaterialDescription<MeshInput>, meshInfo : MeshInfo) =
    inherit BaseMeshRendererComponent (group, material, meshInfo.ToMesh ())

let assetBehavior (am : AssetManager) f =
    Behavior.handleComponentAdded (fun ent (comp : BaseMeshRendererComponent) _ _ ->
        let layer = comp.Layer
        let mesh = comp.BaseMesh

        let material =
            match comp.Material with
            | None ->
                let material = am.GetMaterial comp.MaterialDescription
                comp.Material <- Some material
                material
            | Some material -> material

        f layer material mesh
    )

let private assetRenderBehavior (am : AssetManager) (renderer : Renderer) =
    assetBehavior am (fun layer material mesh ->
        renderer.AddMesh (layer, material.Shader, material.Texture.Buffer, mesh) |> ignore
    )

let create (gl: IGL) fileReadAllText (am : AssetManager) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create (gl, fileReadAllText)

    let materialLookup = Dictionary<IMaterialDescription, BaseMaterial> ()

    Behavior.merge
        [
            assetRenderBehavior am renderer

            Behavior.handleComponentAdded (fun ent (comp : CameraComponent) _ _ ->
                comp.RenderCamera <- renderer.CreateCamera (Matrix4x4.Identity, comp.Projection) |> Some
            )

            Behavior.update (fun ((time, deltaTime): float32 * float32) em _ ->

                let mutable g_view = Matrix4x4.Identity
                let mutable g_projection = Matrix4x4.Identity

                em.ForEach<CameraComponent, TransformComponent> (fun ent cameraComp transformComp ->
                    let heightOffset = Mathf.lerp cameraComp.HeightOffsetLerp cameraComp.HeightOffset deltaTime

                    let projection = cameraComp.Projection
                    let mutable transform = Matrix4x4.Lerp (transformComp.TransformLerp, transformComp.Transform, deltaTime)

                    let mutable v = transform.Translation

                    v.Z <- zEasing.Update (transformComp.Position.Z, time)

                    transform.Translation <- v + Vector3(0.f,0.f,heightOffset)

                    let mutable invertedTransform = Matrix4x4.Identity

                    Matrix4x4.Invert(transform, &invertedTransform) |> ignore

                    let invertedTransform = invertedTransform

                    match cameraComp.RenderCamera with
                    | Some camera ->
                        camera.View <- invertedTransform
                        camera.Projection <- projection
                    | _ -> ()
                )

                renderer.Draw time

                gl.Swap ()
            )

        ]
