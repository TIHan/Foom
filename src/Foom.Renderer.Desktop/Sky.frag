#version 330 core

uniform sampler2D uni_texture;

in vec4 texCoords;

out vec4 color;

void main ()
{
	vec2 longitudeLatitude = vec2((atan(texCoords.y, texCoords.x) / 3.1415926 + 1.0) * 0.5,
                                  (asin(texCoords.z) / 3.1415926 + 0.5));

    color =  texture(uni_texture, vec2(longitudeLatitude.x, -longitudeLatitude.y));
}