module Foom.Renderer.RendererSystem

open System
open System.Reflection
open System.Numerics
open System.IO
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Common.Components
open Foom.Renderer
open Foom.Game.Assets

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
type BaseRenderComponent (pipelineName: string, texture: Texture, mesh: Mesh, extraResource: GpuResource) =
    inherit Component ()

    member val PipelineName = pipelineName

    member val Texture = texture

    member val Mesh = mesh

    member val ExtraResource = extraResource

[<AbstractClass>]
type RenderComponent<'T when 'T :> GpuResource> (pipelineName, texturePath, mesh, extra: 'T) =
    inherit BaseRenderComponent (pipelineName, texturePath, mesh, extra)

    member val Extra = extra

[<Sealed>]
type MeshRenderComponent (pipelineName, texture, meshInfo: MeshInfo) =
    inherit RenderComponent<UnitResource> (pipelineName, texture, meshInfo.ToMesh (), UnitResource ())

let handleMeshRender loadTextureFile (renderer: Renderer) =
    Behavior.handleEvent (fun (evt: Foom.Ecs.Events.AnyComponentAdded) _ em ->
        if typeof<BaseRenderComponent>.GetTypeInfo().IsAssignableFrom(evt.ComponentType.GetTypeInfo()) then
            match em.TryGet (evt.Entity, evt.ComponentType) with
            | Some comp ->
                let meshRendererComp = comp :?> BaseRenderComponent
                let pipelineName = meshRendererComp.PipelineName
                let texture = meshRendererComp.Texture
                let mesh = meshRendererComp.Mesh

                (*
                This will be replaced by an asset management system.
                *)
                if not texture.Buffer.HasData then
                    texture.Buffer.Set (loadTextureFile texture.AssetPath)
                (**)

                renderer.TryAddMesh (pipelineName, texture.Buffer, mesh, meshRendererComp.ExtraResource) |> ignore
            | _ -> ()
    )

let create worldPipeline subPipelines (gl: IGL) fileReadAllText loadTextureFile : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create (gl, fileReadAllText, subPipelines, worldPipeline)

    Behavior.merge
        [
            handleMeshRender loadTextureFile renderer

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
