module Foom.Client.Sky

open System.Numerics

open Foom.Renderer
open Foom.Renderer.RendererSystem
open Foom.Game.Assets

let octahedron_vtx = 
    [|
        Vector3 (0.0f, -1.0f,  0.0f)
        Vector3 (1.0f,  0.0f,  0.0f)
        Vector3 (0.0f,  0.0f,  1.0f)
        Vector3 (-1.0f, 0.0f,  0.0f)
        Vector3 (0.0f,  0.0f, -1.0f)
        Vector3 (0.0f,  1.0f,  0.0f)
    |]

let octahedron_idx =
    [|
        0; 1; 2;
        0; 2; 3;
        0; 3; 4;
        0; 4; 1;
        1; 5; 2;
        2; 5; 3;
        3; 5; 4;
        4; 5; 1;
    |]

let sphere =
    let vertices =
        octahedron_idx
        |> Array.map (fun i -> octahedron_vtx.[i])

    let trianglesLength = vertices.Length / 3
    let triangles = Array.zeroCreate<Vector3 * Vector3 * Vector3> trianglesLength

    for i = 0 to trianglesLength - 1 do
        let v1 = vertices.[0 + (i * 3)]
        let v2 = vertices.[1 + (i * 3)]
        let v3 = vertices.[2 + (i * 3)]
        triangles.[i] <- (v1, v2, v3)
                   

    let rec buildSphere n triangles =
        match n with
        | 3 -> triangles
        | _ ->
            triangles
            |> Array.map (fun (v1: Vector3, v2: Vector3, v3: Vector3) ->                               
                let v1 = v1 |> Vector3.Normalize
                let v2 = Vector3.Normalize v2
                let v3 = Vector3.Normalize v3
                let v12 = v2 * 0.5f + v1 * 0.5f |> Vector3.Normalize
                let v13 = v1 * 0.5f + v3 * 0.5f |> Vector3.Normalize
                let v23 = v2 * 0.5f + v3 * 0.5f |> Vector3.Normalize
                [|
                (v1, v12, v13)
                (v2, v23, v12)
                (v3, v13, v23)
                (v12, v23, v13)
                |]
            )
            |> Array.reduce Array.append
            |> buildSphere (n + 1)

    let triangles = buildSphere (-1) triangles

    let vertices =
        triangles
        |> Array.map (fun (x, y, z) -> [|x;y;z|])
        |> Array.reduce Array.append

    let triangleNormal (v1, v2, v3) = Vector3.Cross (v2 - v1, v3 - v1) |> Vector3.Normalize

    let normals =
        vertices
        |> Array.map (fun v ->
            match triangles |> Array.filter (fun (v1, v2, v3) -> v.Equals v1 || v.Equals v2 || v.Equals v3) with
            | trs ->
                trs
                |> Array.map triangleNormal
                |> Array.reduce ((+))
                |> Vector3.Normalize
        )

    vertices, normals

let vertices, normals = sphere

let skyVertices =
    vertices
    |> Array.rev

type SkyInput (shaderInput) =
    inherit MeshInput (shaderInput)

    member val Model = shaderInput.CreateUniformVar<Matrix4x4> ("uni_model")

let shader = CreateShader SkyInput 2 (CreateShaderPass (fun _ -> [ Stencil2 ]) "Sky")//CreateShader "Sky" 2 ShaderPass.Stencil2 SkyInput

type Sky () =
    inherit Mesh<SkyInput> (skyVertices, [||], [||])

    member val Model = Matrix4x4.CreateScale (100.f)

    override this.SetShaderInput input =
        base.SetShaderInput input

        input.Model.Set this.Model

type SkyRendererComponent (texture) =
    inherit MeshRendererComponent<SkyInput, Sky> (0, MaterialDescription (shader, texture), Sky ())
