namespace Foom.Renderer

open System
open System.Numerics

type Sampler2D =
    {
        mutable TextureId: int
    }

type Uniform<'T> =
    {
        mutable Id: int
        mutable Value: 'T
        mutable SetDirty: unit -> unit
    }

[<RequireQualifiedAccess>]
module Uniform =

    let mat4x4 initialValue =
        {
            Id = 0
            Value = initialValue
            SetDirty = id
        }

    let vec4 initialValue =
        {
            Id = 0
            Value = initialValue
            SetDirty = id
        }

    let sampler2D initialValue =
        {
            Id = 0
            Value = { TextureId = 0 }
            SetDirty = id
        }

type VertexAttribute<'T> =
    {
        mutable Id: int
        mutable BufferId: int
        mutable Value: 'T []
        mutable Count: int
        mutable SetDirty: unit -> unit
    }

[<RequireQualifiedAccess>]
module VertexAttribute =

    let vec3 buffer =
        {
            Id = 0
            BufferId = 0
            Value = buffer
            Count = 0
            SetDirty = id
        }

    let vec2 buffer =
        {
            Id = 0
            BufferId = 0
            Value = buffer
            Count = 0
            SetDirty = id
        }

type ShaderProgram<'T> =
    {
        InitList: (unit -> unit) ResizeArray
        mutable ProgramId: int

        UniformDirtyList: bool ResizeArray 
        UniformBindList: (unit -> unit) ResizeArray

        mutable TextureCount: int
        mutable NextAttribId: int

        VertexAttribDirtyList: bool ResizeArray
        VertexAttribBindList: (unit -> unit) ResizeArray
        VertexAttribUnbindList: (unit -> unit) ResizeArray
        VertexAttribCountList: (unit -> int) ResizeArray
    }

module ShaderProgram =

    let inline addUniform name (uni: Uniform<_>) (shaderProgram: ShaderProgram<_>) =
        let index = shaderProgram.UniformDirtyList.Count

        uni.SetDirty <- 
            fun () -> shaderProgram.UniformDirtyList.[index] <- true

        shaderProgram.UniformDirtyList.Add (true)

        shaderProgram.InitList.Add (
            fun () ->
                uni.Id <- Renderer.getUniformLocation shaderProgram.ProgramId name
        )

    let inline addAttrib name (va: VertexAttribute<_>) (shaderProgram: ShaderProgram<_>) =
        let index = shaderProgram.VertexAttribDirtyList.Count

        va.SetDirty <- 
            fun () -> shaderProgram.VertexAttribDirtyList.[index] <- true

        shaderProgram.VertexAttribDirtyList.Add (true)

        shaderProgram.InitList.Add (
            fun () ->
                va.Id <- Renderer.getAttributeLocation shaderProgram.ProgramId name
        )

        shaderProgram.VertexAttribCountList.Add (fun () -> va.Count)

    ///
    let uniform_mat4x4 name (uni: Matrix4x4 Uniform) (shaderProgram: ShaderProgram<_>) =
        addUniform name uni shaderProgram

        //

        shaderProgram.UniformBindList.Add (
            fun () ->
                Renderer.bindUniformMatrix4x4 uni.Id uni.Value
        )

    let uniform_vec4 name (uni: Vector4 Uniform) (shaderProgram: ShaderProgram<_>) =
        addUniform name uni shaderProgram

        //

        shaderProgram.UniformBindList.Add (
            fun () ->
                Renderer.bindUniformVector4 uni.Id uni.Value
        )

    let uniform_sampler2D name (uni: Sampler2D Uniform) (shaderProgram: ShaderProgram<_>) =
        addUniform name uni shaderProgram

        //

        let n = shaderProgram.TextureCount
        shaderProgram.UniformBindList.Add (
            fun () ->
                Renderer.bindTexture2D uni.Value.TextureId n
                Renderer.bindUniformInt uni.Id n
        )

    ///
    let vertex_vec3 name (va: Vector3 VertexAttribute) (shaderProgram: ShaderProgram<_>) =
        addAttrib name va shaderProgram

        //

        shaderProgram.VertexAttribBindList.Add (
            fun () ->
                Renderer.bindArrayBuffer (va.BufferId)
                Renderer.bindVertexAttributeVector3 (va.Id)
        )

    let vertex_vec2 name (va: Vector2 VertexAttribute) (shaderProgram: ShaderProgram<_>) =
        addAttrib name va shaderProgram

        //

        shaderProgram.VertexAttribBindList.Add (
            fun () ->
                Renderer.bindArrayBuffer (va.BufferId)
                Renderer.bindVertexAttributeVector2 (va.Id)
        )

    let load (vertexSource: string) (fragmentSource: string) (shaderProgram: ShaderProgram<_>) =
        let encoding = System.Text.Encoding.UTF8
        let programId = 
            Renderer.loadShaders 
                (encoding.GetBytes(vertexSource)) 
                (encoding.GetBytes(fragmentSource))

        shaderProgram.ProgramId <- programId


    let run (shaderProgram: ShaderProgram<_>) =
        if shaderProgram.ProgramId > 0 then
            for i = 0 to shaderProgram.InitList.Count - 1 do
                let f = shaderProgram.InitList.[i]
                f ()
            shaderProgram.InitList.Clear ()

            for i = 0 to shaderProgram.UniformDirtyList.Count - 1 do
                let isDirty = shaderProgram.UniformDirtyList.[i]

                if isDirty then
                    let f = shaderProgram.UniformBindList.[i]
                    f ()

            let mutable minCount = 0
            for i = 0 to shaderProgram.VertexAttribDirtyList.Count - 1 do
                let isDirty = shaderProgram.VertexAttribDirtyList.[i]

                let f = shaderProgram.VertexAttribCountList.[i]
                let count = f ()
                if count < minCount || minCount = 0 then
                    minCount <- count

                let f = shaderProgram.VertexAttribBindList.[i]
                f ()

            Renderer.drawTriangles 0 minCount


        

(* Concept API *)
(*

type QuadShader =
    {
        Projection: Matrix4x4 Uniform
        Position: Vector3 VertexAttribute
        UV: Vector2 VertexAttribute
        Color: Vector4 Uniform
        Texture: Sampler2D Uniform
    }

let quad shader =
    programInput
        [
            uniform_mat4x4      "uni_projection" shader.Projection
            in_vec3             "in_position" shader.Position
            in_vec2             "in_uv" shader.UV
            uniform_vec4        "uni_color" shader.Color
            uniform_sampler2D   "uni_texture" shader.Texture
        ]

// Composition

type DoomWallShader =
    {
        Quad: QuadShader
        StretchUpAmount: int Uniform
        StretchDownAmount: int Uniform
    }

let doomWall shader =
    programInput
        [
            quad shader.Quad
            uniform_int     "uni_stretchUpAmount" shader.StretchUpAmount
            uniform_int     "unit_stretchDownAmount" shader.StretchDownAmount

        ]

*)
