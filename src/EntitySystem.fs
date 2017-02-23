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

type SpriteInfo =
    {
        Center: Vector3 []
    }

type Sprite =
    {
        Center: Vector3Buffer
    }

type SkyInfo = SkyInfo of unit

type Sky = Sky of unit

[<Sealed>]
type MeshRendererComponent (meshInfo: MeshInfo) =

    member val MeshInfo = meshInfo

    member val Mesh : Mesh =
        let color =
            meshInfo.Color
            |> Array.map (fun c ->
                Vector4 (
                    single c.R / 255.f,
                    single c.G / 255.f,
                    single c.B / 255.f,
                    single c.A / 255.f)
            )

        Mesh (meshInfo.Position, meshInfo.Uv, color)

    interface IComponent

//[<Sealed>]
//type ExtraRendererComponent<'ExtraInfo, 'Extra> (extraInfo: 'ExtraInfo) =

//    member val ExtraInfo = extraInfo

//type SpriteInput (shaderProgram) =
//    inherit MeshInput (shaderProgram)

//    member val Center = shaderProgram.CreateVertexAttributeVector3 ("in_center")

//type SpriteRendererComponent (meshInfo, spriteInfo) =
//    inherit BaseMeshRendererComponent<SpriteInfo, Sprite> (meshInfo, spriteInfo)

//    override val Extra = 
//        {
//            Center = Buffer.createVector3 (spriteInfo.Center)
//        }


let create worldPipeline subPipelines (app: Application) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create (subPipelines, worldPipeline)

    Behavior.merge
        [
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
