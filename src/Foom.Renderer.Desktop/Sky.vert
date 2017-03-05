#version 330 core

in vec3 position;
in vec2 in_uv;

uniform mat4x4 uni_projection;
uniform mat4x4 uni_view;

out vec2 uv;

void main ()
{
	vec4 position_worldspace = uni_projection * uni_view * vec4(position, 1.0);

	gl_Position = position_worldspace - vec4(uni_view[2].xyz, 0.0);

    uv = in_uv;
}
