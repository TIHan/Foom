#version 330 core

precision highp float;

uniform sampler2D uni_texture;

in vec2 uv;
in vec4 color;
in vec4 lightLevel;

out vec4 outColor;

void main()
{
    vec4 newColor = texture(uni_texture, uv) * lightLevel;
	if(newColor.a < 0.5)
		discard;
	outColor = newColor;
}