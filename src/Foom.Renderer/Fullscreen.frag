#version 330 core

in vec2 uv;

out vec4 color;

uniform sampler2D uni_texture;
uniform float time;

void main ()
{
    color = texture(uni_texture, uv + 0.005*vec2( sin(time+1280.0*uv.x),cos(time+720.0*uv.y)) );
}
