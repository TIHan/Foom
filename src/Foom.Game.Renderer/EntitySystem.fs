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

//[<AbstractClass>]
//type BaseMeshRendererComponent (layer : int, materialDesc : MaterialDescription, mesh : BaseMesh) =
//    inherit Component ()

//    member val Layer = layer

//    member val MaterialDescription = materialDesc

//    member val BaseMesh = mesh

//    member val Material = None with get, set

//    member this.LoadMaterial (am : AssetManager) =
//        match this.Material with
//        | None ->
//            let material = am.GetMaterial materialDesc
//            this.Material <- Some material
//        | _ -> ()

//    abstract member AddMesh : Renderer -> unit

//[<AbstractClass>]
//type MeshRendererComponent<'T, 'U when 'T :> MeshInput and 'U :> Mesh<'T>> (layer, materialDesc : MaterialDescription<'T>, mesh : 'U) =
//    inherit BaseMeshRendererComponent (layer, materialDesc, mesh)

//    member val Mesh = mesh

//    override this.AddMesh renderer =
//        match this.Material with
//        | Some material ->
//            renderer.AddMesh (layer, material.Shader :?> Shader<'T>, material.Texture.Buffer, mesh) |> ignore
//        | _ -> ()

//[<Sealed>]
//type MeshRendererComponent (group, material : MaterialDescription<MeshInput>, meshInfo : MeshInfo) =
//    inherit MeshRendererComponent<MeshInput, Mesh<MeshInput>> (group, material, meshInfo.ToMesh ())

//let assetBehavior (am : AssetManager) =
//    Behavior.ComponentAdded (fun _ _ (comp : BaseMeshRendererComponent) ->
//        comp.LoadMaterial am
//    )

//let assetRenderBehavior (renderer : Renderer) =
//    Behavior.ComponentAdded (fun _ _ (comp : BaseMeshRendererComponent) ->
//        comp.AddMesh (renderer)
//    )

let create (gl: IGL) fileReadAllText (am : AssetManager) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create (gl, fileReadAllText)

    Behavior.Merge
        [
            //assetBehavior am
            //assetRenderBehavior renderer

            Behavior.ComponentAdded (fun _ _ (comp : CameraComponent) ->
                comp.RenderCamera <- renderer.CreateCamera (Matrix4x4.Identity, comp.Projection) |> Some
            )

            Behavior.Update (fun ((time, deltaTime): float32 * float32) em _ ->

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
