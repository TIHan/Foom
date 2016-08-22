namespace Foom.Renderer

open System

(* Concept API *)
(*

type QuadShader =
    {
        Projection: Matrix4x4 Var
        Position: Vector3 [] Var
        UV: Vector2 [] Var
        Color: Vector4 Var
        Texture: Texture Var
    }

let quad shader =
    program "quad.vsh" "quad.fsh" 
        [
            uniform_mat4x4      "uni_projection" shader.Projection.Val
            in_vec3             "in_position" shader.Position.Val
            in_vec2             "in_uv" shader.UV.Val
            uniform_vec4        "uni_color" shader.Color.Val
            uniform_sampler2D   "uni_texture" shader.Texture.Val
        ]

// Composition

type DoomWallShader =
    {
        Quad: QuadShader
        StretchUpAmount: int Var
        StretchDownAmount: int Var
    }

let doomWall shader =
    program "doomWall.vsh" "doomWall.fsh"
        [
            quad shader.Quad
        ]

*)
