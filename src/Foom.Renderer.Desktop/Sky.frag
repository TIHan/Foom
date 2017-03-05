#version 330 core

out vec4 color;

uniform sampler2D uni_texture;

void main ()
{
	color = texture (uni_texture, vec2(0));
}
