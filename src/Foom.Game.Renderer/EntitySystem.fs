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

type MeshInfo =
    {
        Position: Vector3 []
        Uv: Vector2 []
        Color: Vector4 []
    }

    member this.ToMesh () =
        let color =
            this.Color
            |> Array.map (fun c ->
                Vector4 (
                    single c.X / 255.f,
                    single c.Y / 255.f,
                    single c.Z / 255.f,
                    single c.W / 255.f)
            )
        Mesh (this.Position, this.Uv, color)

[<AbstractClass>]
type BaseMaterial (shader, texture) =

    member val Shader = shader

    member val Texture = texture

[<Sealed>]
type Material<'T when 'T :> MeshInput> (shader : Shader<'T>, texture) =
    inherit BaseMaterial (shader :> BaseShader, texture)

[<AbstractClass>]
type BaseMeshRendererComponent (layer : int, material : BaseMaterial, mesh : BaseMesh) =
    inherit Component ()

    member val Layer = layer

    member val Material = material

    member val BaseMesh = mesh

[<AbstractClass>]
type MeshRendererComponent<'T, 'U when 'T :> MeshInput and 'U :> Mesh<'T>> (group, material : Material<'T>, mesh : 'U) =
    inherit BaseMeshRendererComponent (group, material, mesh)

    member val Mesh = mesh

[<Sealed>]
type MeshRendererComponent (group, material, meshInfo : MeshInfo) =
    inherit BaseMeshRendererComponent (group, material, meshInfo.ToMesh ())

let create (gl: IGL) fileReadAllText (am : AssetManager) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create (gl, fileReadAllText)

    Behavior.merge
        [
            Behavior.handleComponentAdded (fun ent (comp : CameraComponent) _ _ ->
                comp.RenderCamera <- renderer.CreateCamera (Matrix4x4.Identity, comp.Projection) |> Some
            )

            Behavior.handleComponentAdded (fun ent (comp : BaseMeshRendererComponent) _ _ ->
                let layer = comp.Layer
                let material = comp.Material
                let mesh = comp.BaseMesh

                am.LoadTexture (material.Texture)

                renderer.AddMesh (layer, material.Shader, material.Texture.Buffer, mesh) |> ignore
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
