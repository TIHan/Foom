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
    }

type TextureInfo =
    {
        TexturePath: string
    }

type MaterialInfo =
    {
        TextureInfo: TextureInfo
        ShaderName: string
    }

type RenderInfo =
    {
        MeshInfo: MeshInfo
        MaterialInfo: MaterialInfo
    }

type RenderBatchInfo =
    {
        MaterialInfo: MaterialInfo
        MeshInfos: MeshInfo ResizeArray
    }

type RenderInfoComponent (renderInfo) =

    member val RenderInfo = renderInfo

    interface IComponent

type SpriteComponent =
    {
        Center: Vector3Buffer
    }

    interface IComponent

type SpriteInfoComponent =
    {
        Center: Vector3 []
    }

    interface IComponent

let renderer = Renderer.Create ()
let functionCache = Dictionary<string, (EntityManager -> Entity -> Renderer -> obj) * (ShaderProgram -> obj -> unit)> ()
let shaderCache = Dictionary<string, Shader> ()
let textureCache = Dictionary<string, Texture> ()

let handleSomething () =
    Behavior.handleEvent (fun (evt: Events.ComponentAdded<RenderInfoComponent>) ((time, deltaTime): float32 * float32) em ->
        em.TryGet<RenderInfoComponent> (evt.Entity)
        |> Option.iter (fun comp ->
            let info = comp.RenderInfo

            let mesh = renderer.CreateMesh (info.MeshInfo.Position, info.MeshInfo.Uv, info.MeshInfo.Color)
            let shaderName = info.MaterialInfo.ShaderName

            let vertexShaderFile = shaderName + ".vert"

            let fragmentShaderFile = shaderName + ".frag"

            let shader, f =
                match shaderCache.TryGetValue (shaderName) with
                | true, shader -> shader, (fun _ _ _ -> null)
                | _ -> 

                    let vertexBytes = File.ReadAllText (vertexShaderFile) |> System.Text.Encoding.UTF8.GetBytes
                    let fragmentBytes = File.ReadAllText (fragmentShaderFile) |> System.Text.Encoding.UTF8.GetBytes

                    let f, g =
                        match functionCache.TryGetValue(shaderName) with
                        | true, (f, g) -> f, g
                        | _ -> (fun _ _ _ -> null), (fun _ _ -> ())

                    let shader = renderer.CreateTextureMeshShader (vertexBytes, fragmentBytes, g)

                    shaderCache.Add (shaderName, shader)

                    shader, f

            let texture =
                match textureCache.TryGetValue (info.MaterialInfo.TextureInfo.TexturePath) with
                | true, texture -> texture
                | _ ->

                    let bmp = new Bitmap(info.MaterialInfo.TextureInfo.TexturePath)
                    let texture = renderer.CreateTexture (bmp)

                    textureCache.Add(info.MaterialInfo.TextureInfo.TexturePath, texture)

                    texture
                
            let material = renderer.CreateMaterial (shader, texture)

            renderer.TryAdd (material, mesh, f em evt.Entity renderer)
            |> Option.iter (fun render ->
                em.Add (evt.Entity, RenderComponent (mesh, material, render))
            )
        )
    )

let create (shaders: (string * (EntityManager -> Entity -> Renderer -> obj) * (ShaderProgram -> obj -> unit)) list) (app: Application) : Behavior<float32 * float32> =

    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    shaders
    |> List.iter (fun (key, f, g) ->
        functionCache.[key] <- (f, g)
    )

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

                Backend.draw app

            )

        ]
