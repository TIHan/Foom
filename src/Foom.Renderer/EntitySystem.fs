module Foom.Renderer.RendererSystem

open System
open System.Numerics
open System.IO
open System.Drawing
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Common.Components

type MeshInfo =
    {
        Position: Vector3 []
        Uv: Vector2 []
        Color: Color []

        Texture: string
        SubRenderer: string
    }

    member this.ToMesh () =
        let color =
            this.Color
            |> Array.map (fun c ->
                Vector4 (
                    single c.R / 255.f,
                    single c.G / 255.f,
                    single c.B / 255.f,
                    single c.A / 255.f)
            )
        Mesh (this.Position, this.Uv, color)

type SkyInfo = SkyInfo of unit

type Sky = Sky of unit

[<AbstractClass>]
type BaseRenderComponent (subRenderer: string, texture: string, mesh: Mesh, extraResource: GpuResource) =
    inherit Component ()

    member val SubRenderer = subRenderer

    member val Texture = texture

    member val Mesh = mesh

    member val ExtraResource = extraResource

[<AbstractClass>]
type RenderComponent<'T when 'T :> GpuResource> (subRenderer, texture, mesh, extra: 'T) =
    inherit BaseRenderComponent (subRenderer, texture, mesh, extra)

    member val Extra = extra

[<Sealed>]
type MeshRenderComponent (meshInfo: MeshInfo) =
    inherit RenderComponent<UnitResource> (meshInfo.SubRenderer, meshInfo.Texture, meshInfo.ToMesh (), UnitResource ())

let handleMeshRender (renderer: Renderer) =
    Behavior.handleEvent (fun (evt: Foom.Ecs.Events.AnyComponentAdded) _ em ->
        if typeof<BaseRenderComponent>.IsAssignableFrom(evt.ComponentType) then
            match em.TryGet (evt.Entity, evt.ComponentType) with
            | Some comp ->
                let meshRendererComp = comp :?> BaseRenderComponent
                let mesh = meshRendererComp.Mesh
                let texture = meshRendererComp.Texture
                let subRenderer = meshRendererComp.SubRenderer
                renderer.TryAddMesh (texture, mesh, subRenderer) |> ignore
            | _ -> ()
    )

let create worldPipeline subPipelines (app: Application) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create (subPipelines, worldPipeline)

    Behavior.merge
        [
            handleMeshRender renderer

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

                Backend.draw app
            )

        ]
