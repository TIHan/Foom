#version 330 core

in vec3 position;

uniform mat4x4 uni_projection;
uniform mat4x4 uni_view;

void main ()
{
	mat4x4 view = uni_view;

	//view[3] = vec4(0.0, 0.0, 0.0, 1.0);

	vec4 position_worldspace = uni_projection * view * vec4(position, 1.0);

	gl_Position = position_worldspace; //- vec4(uni_view[0].xyz, 0.0);
}
