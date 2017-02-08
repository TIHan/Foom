#version 330 core

in vec2 uv;

out vec4 color;

uniform sampler2D uni_texture;

void main ()
{
	color = texture (uni_texture, uv);
}