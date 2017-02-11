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
        LayerIndex: int
    }

type MeshRenderComponent (renderInfo) =

    member val RenderInfo = renderInfo

    member val Mesh : Mesh =
        let color =
            renderInfo.MeshInfo.Color
            |> Array.map (fun c ->
                Vector4 (
                    single c.R / 255.f,
                    single c.G / 255.f,
                    single c.B / 255.f,
                    single c.A / 255.f)
            )
        {
            Position = Buffer.createVector3 (renderInfo.MeshInfo.Position)
            Uv = Buffer.createVector2 (renderInfo.MeshInfo.Uv)
            Color = Buffer.createVector4 (color) 
        }

    interface IComponent

type SpriteComponent (center) =

    member val Center = Buffer.createVector3 (center)

    interface IComponent

type FunctionCache = Dictionary<string, (EntityManager -> Entity -> Renderer -> obj) * (ShaderProgram -> obj -> (RenderPass -> unit) -> unit)>
type ShaderCache = Dictionary<string, ShaderId>
type TextureCache = Dictionary<string, Texture>

let handleSomething (functionCache: FunctionCache) (shaderCache: ShaderCache) (textureCache: TextureCache) (renderer: Renderer) =
    Behavior.handleEvent (fun (evt: Events.ComponentAdded<MeshRenderComponent>) _ em ->
        em.TryGet<MeshRenderComponent> (evt.Entity)
        |> Option.iter (fun comp ->

            let info = comp.RenderInfo
            let shaderName = info.MaterialInfo.ShaderName.ToUpper ()

            let vertexShaderFile = shaderName + ".vert"

            let fragmentShaderFile = shaderName + ".frag"

            let shaderId, f =
                match shaderCache.TryGetValue (shaderName) with
                | true, shader ->

                    let f, _ =
                        match functionCache.TryGetValue(shaderName) with
                        | true, (f, g) -> f, g
                        | _ -> (fun _ _ _ -> null), (fun _ _ run -> run RenderPass.Depth)

                    shader, f
                | _ -> 

                    let vertexBytes = File.ReadAllText (vertexShaderFile) |> System.Text.Encoding.UTF8.GetBytes
                    let fragmentBytes = File.ReadAllText (fragmentShaderFile) |> System.Text.Encoding.UTF8.GetBytes

                    let f, g =
                        match functionCache.TryGetValue(shaderName) with
                        | true, (f, g) -> f, g
                        | _ -> (fun _ _ _ -> null), (fun _ _ run -> run RenderPass.Depth)

                    let shader = renderer.CreateTextureMeshShader (vertexBytes, fragmentBytes, g)

                    shaderCache.Add (shaderName, shader)

                    shader, f

            let texture =
                match textureCache.TryGetValue (info.MaterialInfo.TextureInfo.TexturePath) with
                | true, texture -> texture
                | _ ->

                    let bmp = new Bitmap(info.MaterialInfo.TextureInfo.TexturePath)

                    let buffer = Texture2DBuffer ([||], 0, 0)
                    buffer.Set bmp

                    let texture =
                        {
                            Buffer = buffer
                        }

                    textureCache.Add(info.MaterialInfo.TextureInfo.TexturePath, texture)

                    texture

            renderer.TryAdd (shaderId, texture, comp.Mesh, f em evt.Entity renderer, info.LayerIndex) |> ignore
        )
    )

let handleCamera (renderer: Renderer) =
    Behavior.handleEvent (fun (evt: Events.ComponentAdded<CameraComponent>) ((time, deltaTime): float32 * float32) em ->
        em.TryGet<CameraComponent> (evt.Entity)
        |> Option.iter (fun cameraComp ->
            match renderer.TryCreateRenderCamera Matrix4x4.Identity cameraComp.Projection cameraComp.LayerMask cameraComp.ClearFlags cameraComp.Depth with
            | Some renderCamera ->
                cameraComp.RenderCamera <- renderCamera
            | _ -> ()
        )
    )

let create (shaders: (string * (EntityManager -> Entity -> Renderer -> obj) * (ShaderProgram -> obj -> (RenderPass -> unit) -> unit)) list) (app: Application) : Behavior<float32 * float32> =

    // This should probably be on the camera itself :)
    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    let renderer = Renderer.Create ()
    let functionCache = Dictionary<string, (EntityManager -> Entity -> Renderer -> obj) * (ShaderProgram -> obj -> (RenderPass -> unit) -> unit)> ()
    let shaderCache = Dictionary<string, ShaderId> ()
    let textureCache = Dictionary<string, Texture> ()

    shaders
    |> List.iter (fun (key, f, g) ->
        functionCache.[key.ToUpper()] <- (f, g)
    )

    Behavior.merge
        [
            handleCamera renderer
            handleSomething functionCache shaderCache textureCache renderer

            Behavior.update (fun ((time, deltaTime): float32 * float32) em _ ->

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

                    cameraComp.RenderCamera.view <- invertedTransform
                    cameraComp.RenderCamera.projection <- projection
                )

                renderer.Draw time

                Backend.draw app
            )

        ]
