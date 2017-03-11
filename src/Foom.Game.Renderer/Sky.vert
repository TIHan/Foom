#version 330 core

in vec3 position;

uniform mat4x4 uni_projection;
uniform mat4x4 uni_view;
uniform mat4x4 uni_model;

out vec4 texCoords;

void main ()
{
	mat4x4 view = uni_view;

	view[3] = vec4(0.0, 0.0, 0.0, 1.0);

	vec4 position_worldspace = uni_projection * view * uni_model * vec4(position, 1.0);

	gl_Position = position_worldspace;

	texCoords = vec4(position, 1.0);
}
