module Foom.Renderer.RendererSystem

open System
open System.Numerics
open System.IO
open System.Drawing
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Common.Components

type RenderComponent (mesh, material, textureMeshId) =

    member val Mesh : Mesh = mesh

    member val Material : Material = material

    member val TextureMeshId : TextureMeshId = textureMeshId

    interface IComponent

type MeshInfo =
    {
        Position: Vector3 []
        Uv: Vector2 []
        Color: Color []
        Center: Vector3 []
        IsWireframe: bool
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
    }

type RenderInfo =
    {
        MeshInfo: MeshInfo
        MaterialInfo: MaterialInfo
        Data: obj
    }

type RenderBatchInfo =
    {
        MaterialInfo: MaterialInfo
        MeshInfos: MeshInfo ResizeArray
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
            let info = comp.RenderInfo

            let mesh = renderer.CreateMesh (info.MeshInfo.Position, info.MeshInfo.Uv, info.MeshInfo.Color, info.MeshInfo.Center, info.MeshInfo.IsWireframe)

            let vertexShader =
                if info.MeshInfo.IsWireframe then
                    "wireframe.vertex"
                else
                    info.MaterialInfo.ShaderInfo.VertexShader

            let fragmentShader =
                if info.MeshInfo.IsWireframe then
                    "wireframe.fragment"
                else
                    info.MaterialInfo.ShaderInfo.FragmentShader

            let shader =
                match shaderCache.TryGetValue ((vertexShader, fragmentShader)) with
                | true, shader -> shader
                | _ -> 
                    let vertexFile = vertexShader
                    let fragmentFile = fragmentShader

                    let vertexBytes = File.ReadAllText (vertexFile) |> System.Text.Encoding.UTF8.GetBytes
                    let fragmentBytes = File.ReadAllText (fragmentFile) |> System.Text.Encoding.UTF8.GetBytes

                    let shader = renderer.CreateTextureMeshShader (vertexBytes, fragmentBytes)

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
                
            let material = renderer.CreateMaterial (shader, texture)

            renderer.TryAdd (material, mesh, info.Data)
            |> Option.iter (fun render ->
                em.Add (evt.Entity, RenderComponent (mesh, material, render))
            )

            em.Remove<RenderInfoComponent> (evt.Entity)
        )
    )

let create (app: Application) : Behavior<float32 * float32> =

    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    Behavior.merge
        [
            handleSomething ()

            Behavior.update (fun ((time, deltaTime): float32 * float32) entityManager eventManager ->

                renderer.Clear ()

                entityManager.TryFind<CameraComponent> (fun _ _ -> true)
                |> Option.iter (fun (ent, cameraComp) ->

                    entityManager.TryGet<TransformComponent> (ent)
                    |> Option.iter (fun transformComp ->

                        let heightOffset = Mathf.lerp cameraComp.HeightOffsetLerp cameraComp.HeightOffset deltaTime

                        let projection = cameraComp.Projection
                        let mutable transform = Matrix4x4.Lerp (transformComp.TransformLerp, transformComp.Transform, deltaTime)

                        let mutable v = transform.Translation

                        v.Z <- zEasing.Update (transformComp.Position.Z, time)

                        transform.Translation <- v + Vector3(0.f,0.f,heightOffset)

                        let mutable invertedTransform = Matrix4x4.Identity

                        Matrix4x4.Invert(transform, &invertedTransform) |> ignore

                        let invertedTransform = invertedTransform

                        renderer.Draw projection invertedTransform
                    )
                )

                Renderer.draw app

            )

        ]
