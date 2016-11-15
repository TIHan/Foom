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
        GetTransforms: (unit -> Matrix4x4) ResizeArray
        Positions: Vector3ArrayBuffer ResizeArray
        Uvs: Vector2ArrayBuffer ResizeArray
        Colors: Color ResizeArray
        RefIndices: Ref<int> ResizeArray
    }

type RenderState =
    {
        Deletion: Dictionary<Entity, unit -> unit>
        Lookup: Dictionary<int, Dictionary<int, Texture2DBuffer option * RenderBucket>>
    }

    member this.Add (ent, programId, textureId, texture, transformComp, position, uv, color) =
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
                        GetTransforms = ResizeArray ()
                        Positions = ResizeArray ()
                        Uvs = ResizeArray ()
                        Colors = ResizeArray ()
                        RefIndices = ResizeArray ()
                    }

                textureLookup.Add (textureId, (texture, bucket))

                (texture, bucket)

        let indexRef = ref bucket.GetTransforms.Count

        bucket.GetTransforms.Add transformComp
        bucket.Positions.Add position
        bucket.Uvs.Add uv
        bucket.Colors.Add color
        bucket.RefIndices.Add indexRef

        let deletion =
            fun () ->
                let lastIndexRef = bucket.RefIndices.[bucket.RefIndices.Count - 1]

                let originalIndex = !indexRef
                let lastIndex = !lastIndexRef

                lastIndexRef := originalIndex

                bucket.GetTransforms.[originalIndex] <- bucket.GetTransforms.[lastIndex]
                bucket.Positions.[originalIndex] <- bucket.Positions.[lastIndex]
                bucket.Uvs.[originalIndex] <- bucket.Uvs.[lastIndex]
                bucket.Colors.[originalIndex] <- bucket.Colors.[lastIndex]
                bucket.RefIndices.[originalIndex] <- bucket.RefIndices.[lastIndex]

                bucket.GetTransforms.RemoveAt (lastIndex) |> ignore
                bucket.Positions.RemoveAt (lastIndex) |> ignore
                bucket.Uvs.RemoveAt (lastIndex) |> ignore
                bucket.Colors.RemoveAt (lastIndex) |> ignore
                bucket.RefIndices.RemoveAt (lastIndex) |> ignore

        this.Deletion.Add (ent, deletion);
    
    member this.ForEach f =
        this.Lookup
        |> Seq.iter (fun pair ->
            let programId = pair.Key
            let textureLookup = pair.Value

            textureLookup
            |> Seq.iter (fun pair ->
                let textureId = pair.Key
                let (texture, bucket) = pair.Value

                f programId texture bucket
            )
        )
        

// Fixme: global
let state = 
    {
        Deletion = Dictionary ()
        Lookup = Dictionary ()
    }

let render (projection: Matrix4x4) (view: Matrix4x4) (cameraModel: Matrix4x4) (entityManager: EntityManager) =

    Renderer.enableDepth ()

    let mvp = (projection * view) |> Matrix4x4.Transpose

    state.ForEach (fun programId texture bucket ->

        Renderer.useProgram programId

        let count = bucket.Positions.Count

        for i = 0 to count - 1 do

            let getTransform = bucket.GetTransforms.[i]
            let position = bucket.Positions.[i]
            let uv = bucket.Uvs.[i]
            let color = bucket.Colors.[i]

            let model = getTransform ()

            position.TryBufferData () |> ignore
            uv.TryBufferData () |> ignore

            let uniformColor = Renderer.getUniformLocation programId "uni_color"
            let uniformProjection = Renderer.getUniformLocation programId "uni_projection"

            Renderer.setUniformProjection uniformProjection mvp

            position.Bind ()
            Renderer.bindPosition programId

            uv.Bind ()
            Renderer.bindUv programId

            match texture with
            | Some texture ->
                Renderer.setTexture programId texture.Id
                texture.Bind ()
            | _ -> ()

            Renderer.setUniformColor uniformColor (Color.FromArgb (255, int color.R, int color.G, int color.B) |> RenderColor.OfColor)
            Renderer.drawTriangles 0 position.Length
    )

    Renderer.disableDepth ()

    // TODO: To fix wireframe, let's add to our renderstate when to render after the depth buffer.
    entityManager.ForEach<MaterialComponent, WireframeComponent> (fun ent materialComp wireframeComp ->

        wireframeComp.Position.TryBufferData () |> ignore

        let mvp = (projection * view) |> Matrix4x4.Transpose

        let material = materialComp.Material

        material.ShaderProgramId
        |> Option.iter (fun programId ->
            if wireframeComp.Position.Length > 0 then
                Renderer.useProgram programId

                let uniformColor = Renderer.getUniformColor programId
                let uniformProjection = Renderer.getUniformProjection programId

                Renderer.setUniformProjection uniformProjection mvp

                wireframeComp.Position.Bind()
                Renderer.bindPosition programId

                let material = materialComp.Material

                Renderer.setUniformColor uniformColor (Color.FromArgb (255, int material.Color.R, int material.Color.G, int material.Color.B) |> RenderColor.OfColor)
                Renderer.drawArrays 0 wireframeComp.Position.Length
        )
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

        match em.TryGet<TransformComponent> ent with
        | Some transformComp ->

            match em.TryGet<MeshComponent> ent with
            | Some meshComp ->

                let material = materialComp.Material

                let programId =

                    match material.ShaderProgramId with
                    | None ->
                        let vertex = material.VertexShaderFileName
                        let fragment = material.FragmentShaderFileName

                        match shaderCache.TryGetValue ((vertex, fragment)) with
                        | true, programId ->
                            material.ShaderProgramId <- Some programId
                            programId

                        | _ ->
                            let mutable vertexFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (vertex))
                            let mutable fragmentFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (fragment))

                            let programId = Renderer.loadShaders vertexFile fragmentFile
                            material.ShaderProgramId <- Some programId
                            shaderCache.Add ((vertex, fragment), programId)
                            programId

                    | Some programId -> programId

             
                let textureId, texture =
                    match material.Texture with
                    | Some texture ->
                        texture.TryBufferData () |> ignore
                        texture.Id, Some texture
                    | _ ->
                        0, None

                let mesh = meshComp.Mesh
                let getTransform = fun () -> transformComp.Transform
                state.Add (ent, programId, textureId, texture, getTransform, mesh.Position, mesh.Uv, material.Color)

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
