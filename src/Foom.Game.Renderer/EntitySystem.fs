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
type BaseMeshRendererComponent (group, texture, mesh, extraResource: GpuResource) =
    inherit Component ()

    member val Group = group

    member val Texture = texture

    member val Mesh = mesh

    member val ExtraResource = extraResource

[<AbstractClass>]
type MeshRendererComponent<'T when 'T :> GpuResource> (group, texture, mesh, extra : 'T) =
    inherit BaseMeshRendererComponent (group, texture, mesh, extra)

    member val Extra = extra

[<Sealed>]
type MeshRendererComponent (group, texture, meshInfo : MeshInfo) =
    inherit BaseMeshRendererComponent (group, texture, meshInfo.ToMesh (), UnitResource ())

let create worldPipeline subPipelines (gl: IGL) fileReadAllText (am : AssetManager) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create (gl, fileReadAllText, subPipelines, worldPipeline)

    Behavior.merge
        [
            Behavior.handleComponentAdded (fun ent (comp : BaseMeshRendererComponent) _ _ ->
                let group = comp.Group
                let texture = comp.Texture
                let mesh = comp.Mesh

                am.LoadTexture (texture)

                renderer.TryAddMesh (group, texture.Buffer, mesh, comp.ExtraResource) |> ignore
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

                    g_view <- invertedTransform
                    g_projection <- projection
                )

                renderer.Draw time g_view g_projection

                gl.Swap ()
            )

        ]
