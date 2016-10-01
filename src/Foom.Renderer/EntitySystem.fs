module Foom.Renderer.RendererSystem

open System
open System.Numerics
open System.IO
open System.Drawing
open System.Collections.Generic

open Foom.Ecs
open Foom.Math
open Foom.Common.Components

////////

// TODO: We could make this instanced.
type RenderBucket =
    {
        Transforms: TransformComponent ResizeArray
        Positions: Vector3ArrayBuffer ResizeArray
        Uvs: Vector2ArrayBuffer ResizeArray
    }

type RenderState =
    {
        Lookup: Dictionary<int, Dictionary<int, Texture2DBuffer option * RenderBucket>>
    }

    member this.Add (programId, textureId, texture, transformComp, position, uv) =
        let textureLookup =
            match this.Lookup.TryGetValue (programId) with
            | true, lookup -> lookup
            | _, _ ->
                let lookup = Dictionary ()
                this.Lookup.Add (programId, lookup)
                lookup
             
        let (_, bucket) =
            match textureLookup.TryGetValue (textureId) with
            | true, result -> result
            | _, _ ->

                let bucket =
                    {
                        Transforms = ResizeArray ()
                        Positions = ResizeArray ()
                        Uvs = ResizeArray ()
                    }

                textureLookup.Add (textureId, (texture, bucket))

                (texture, bucket)

        bucket.Transforms.Add transformComp
        bucket.Positions.Add position
        bucket.Uvs.Add uv
               
    

        

// Fixme: global
let state = 
    {
        Lookup = Dictionary ()
    }

let render (projection: Matrix4x4) (view: Matrix4x4) (cameraModel: Matrix4x4) (entityManager: EntityManager) =

    Renderer.enableDepth ()

    entityManager.ForEach<MeshComponent, MaterialComponent, TransformComponent> (fun ent meshComp materialComp transformComp ->
        let model = transformComp.Transform

        let mvp = (projection * view) |> Matrix4x4.Transpose

        match materialComp.ShaderProgramState with
        | ShaderProgramState.Loaded programId ->
            meshComp.Position.TryBufferData () |> ignore
            meshComp.Uv.TryBufferData () |> ignore

            Renderer.useProgram programId

            let uniformColor = Renderer.getUniformLocation programId "uni_color"
            let uniformProjection = Renderer.getUniformLocation programId "uni_projection"

            Renderer.setUniformProjection uniformProjection mvp

            meshComp.Position.Bind ()
            Renderer.bindPosition programId

            meshComp.Uv.Bind ()
            Renderer.bindUv programId

            match materialComp.Texture with
            | Some texture ->
                Renderer.setTexture programId texture.Id
                texture.Bind ()
            | _ -> ()

            Renderer.setUniformColor uniformColor (Color.FromArgb (255, int materialComp.Color.R, int materialComp.Color.G, int materialComp.Color.B) |> RenderColor.OfColor)
            Renderer.drawTriangles 0 meshComp.Position.Length
        | _ -> ()
    )

    Renderer.disableDepth ()

    entityManager.ForEach<MaterialComponent, WireframeComponent> (fun ent materialComp wireframeComp ->

        wireframeComp.Position.TryBufferData () |> ignore

        let mvp = (projection * view) |> Matrix4x4.Transpose

        match materialComp.ShaderProgramState with
        | ShaderProgramState.Loaded programId when wireframeComp.Position.Length > 0 ->
            Renderer.useProgram programId

            let uniformColor = Renderer.getUniformColor programId
            let uniformProjection = Renderer.getUniformProjection programId

            Renderer.setUniformProjection uniformProjection mvp

            wireframeComp.Position.Bind()
            Renderer.bindPosition programId

            Renderer.setUniformColor uniformColor (Color.FromArgb (255, int materialComp.Color.R, int materialComp.Color.G, int materialComp.Color.B) |> RenderColor.OfColor)
            Renderer.drawArrays 0 wireframeComp.Position.Length
        | _ -> ()
    )

let componentAddedQueue f =
    Behavior.handleEvent (fun (componentAdded: Events.ComponentAdded<'T>) (_, deltaTime: float32) entityManager ->
        entityManager.TryGet<'T> (componentAdded.Entity)
        |> Option.iter (fun comp ->
            f componentAdded.Entity comp deltaTime entityManager
        )
    )

let shaderCache = Dictionary<string * string, int> ()
let materialQueue =
    componentAddedQueue (fun ent (materialComp: MaterialComponent) deltaTime em ->

        match em.TryGet<MeshComponent> ent with
        | Some meshComp ->

            let programId =
                match materialComp.ShaderProgramState with
                | ShaderProgramState.ReadyToLoad (vertex, fragment) ->

                    match shaderCache.TryGetValue ((vertex, fragment)) with
                    | true, programId ->
                        materialComp.ShaderProgramState <- ShaderProgramState.Loaded programId
                        programId

                    | _ ->
                        let mutable vertexFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (vertex))
                        let mutable fragmentFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (fragment))

                        let programId = Renderer.loadShaders vertexFile fragmentFile
                        materialComp.ShaderProgramState <- ShaderProgramState.Loaded programId
                        shaderCache.Add ((vertex, fragment), programId)
                        programId

                | ShaderProgramState.Loaded programId -> programId

         
            match materialComp.Texture with
            | Some texture ->
                texture.TryBufferData () |> ignore
            | _ -> ()        


        | _ -> ()

    )

let create (app: Application) : ESystem<float32 * float32> =

    let zEasing = Foom.Math.Mathf.LerpEasing(0.100f)

    ESystem.create "Renderer"
        [
            materialQueue

            Behavior.update (fun ((time, deltaTime): float32 * float32) entityManager eventManager ->

                Renderer.clear ()

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

                        render projection invertedTransform transformComp.Transform entityManager
                    )
                )

                Renderer.draw app

            )

        ]
