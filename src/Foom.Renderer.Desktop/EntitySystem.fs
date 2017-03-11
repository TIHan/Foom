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

        Material: Material
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

[<AbstractClass>]
type BaseRenderComponent (material: Material, mesh: Mesh, extraResource: GpuResource) =
    inherit Component ()

    member val Material = material

    member val Mesh = mesh

    member val ExtraResource = extraResource

[<AbstractClass>]
type RenderComponent<'T when 'T :> GpuResource> (material, mesh, extra: 'T) =
    inherit BaseRenderComponent (material, mesh, extra)

    member val Extra = extra

[<Sealed>]
type MeshRenderComponent (meshInfo: MeshInfo) =
    inherit RenderComponent<UnitResource> (meshInfo.Material, meshInfo.ToMesh (), UnitResource ())

let handleMeshRender (renderer: Renderer) =
    Behavior.handleEvent (fun (evt: Foom.Ecs.Events.AnyComponentAdded) _ em ->
        if typeof<BaseRenderComponent>.IsAssignableFrom(evt.ComponentType) then
            match em.TryGet (evt.Entity, evt.ComponentType) with
            | Some comp ->
                let meshRendererComp = comp :?> BaseRenderComponent
                let mesh = meshRendererComp.Mesh
                let material = meshRendererComp.Material

                (*
                This will be replaced by an asset management system.
                *)
                if not material.IsInitialized then
                    material.TextureBuffer.Set (new BitmapTextureFile (material.TexturePath))
                    material.IsInitialized <- true
                (**)

                renderer.TryAddMesh (material, mesh, meshRendererComp.ExtraResource) |> ignore
            | _ -> ()
    )

let create worldPipeline subPipelines (app: Application) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)
    let desktopGL = DesktopGL ()

    let renderer = Renderer.Create (desktopGL, (fun filePath -> File.ReadAllText filePath |> System.Text.Encoding.UTF8.GetBytes), subPipelines, worldPipeline)

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
