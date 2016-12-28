module Foom.Renderer.RendererSystem

open System
open System.Numerics
open System.IO
open System.Drawing
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Common.Components

type RenderComponent (mesh, material, meshRender) =

    member val Mesh : Mesh = mesh

    member val Material : Material = material

    member val MeshRender : MeshRender = meshRender

    interface IComponent

type MeshInfo =
    {
        Position: Vector3 []
        Uv: Vector2 []
    }

type TextureInfo =
    {
        TexturePath: string
    }

type ShaderInfo =
    {
        VertexShader: string
        FragmentShader: string
    }

type MaterialInfo =
    {
        TextureInfo: TextureInfo
        ShaderInfo: ShaderInfo
        Color: Color
    }

type RenderInfo =
    {
        MeshInfo: MeshInfo
        MaterialInfo: MaterialInfo
    }

type RenderInfoComponent (renderInfo) =

    member val RenderInfo = renderInfo

    interface IComponent


let renderer = FRenderer.Create ()
let shaderCache = Dictionary<string * string, Shader> ()
let textureCache = Dictionary<string, Texture> ()

let handleSomething () =
    Behavior.handleEvent (fun (evt: Events.ComponentAdded<RenderInfoComponent>) ((time, deltaTime): float32 * float32) em ->
        em.TryGet<RenderInfoComponent> (evt.Entity)
        |> Option.iter (fun comp ->
            em.TryGet<TransformComponent> (evt.Entity)
            |> Option.iter (fun transformComp ->
                let info = comp.RenderInfo

                let mesh = renderer.CreateMesh (info.MeshInfo.Position, info.MeshInfo.Uv)

                let shader =
                    match shaderCache.TryGetValue ((info.MaterialInfo.ShaderInfo.VertexShader, info.MaterialInfo.ShaderInfo.FragmentShader)) with
                    | true, shader -> shader
                    | _ -> 
                        let vertexFile = info.MaterialInfo.ShaderInfo.VertexShader
                        let fragmentFile = info.MaterialInfo.ShaderInfo.FragmentShader

                        let vertexBytes = File.ReadAllBytes (vertexFile)
                        let fragmentBytes = File.ReadAllBytes (fragmentFile)

                        let shader = renderer.CreateShader (vertexBytes, fragmentBytes)

                        shaderCache.Add ((vertexFile, fragmentFile), shader)

                        shader

                let texture =
                    match textureCache.TryGetValue (info.MaterialInfo.TextureInfo.TexturePath) with
                    | true, texture -> texture
                    | _ ->

                        let bmp = new Bitmap(info.MaterialInfo.TextureInfo.TexturePath)
                        let texture = renderer.CreateTexture (bmp)

                        textureCache.Add(info.MaterialInfo.TextureInfo.TexturePath, texture)

                        texture
                
                let material = renderer.CreateMaterial (shader, texture, info.MaterialInfo.Color)

                renderer.TryAdd (material, mesh, fun () -> transformComp.Transform)
                |> Option.iter (fun render ->
                    em.Add (evt.Entity, RenderComponent (mesh, material, render))
                )
            )
        )
    )

let create (app: Application) : ESystem<float32 * float32> =

    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    ESystem.create "Renderer"
        [
            handleSomething ()

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

                        renderer.Draw projection invertedTransform
                    )
                )

                Renderer.draw app

            )

        ]
