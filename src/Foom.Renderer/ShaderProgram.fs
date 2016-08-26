namespace Foom.Renderer

open System

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
    program
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
    program
        [
            quad shader.Quad
            uniform_int     "uni_stretchUpAmount" shader.StretchUpAmount
            uniform_int     "unit_stretchDownAmount" shader.StretchDownAmount

        ]

*)
