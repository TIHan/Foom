#version 330 core

uniform sampler2D uni_texture;

in vec2 uv;
in vec4 color;

out vec4 outColor;

void main()
{
    vec4 newColor = texture(uni_texture, uv) * color;
	if(newColor.a < 0.5)
		discard;
	outColor = newColor;
}