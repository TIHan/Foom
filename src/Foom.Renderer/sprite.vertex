#version 330 core

in vec3 position;
in vec2 in_uv;
in vec4 in_color;
in vec3 in_center;

uniform mat4x4 uni_projection;
uniform mat4x4 uni_view;

out vec2 uv;
out vec4 color;

void main ()
{
	vec3 CameraRight_worldspace = vec3 (uni_view[0][0], uni_view[1][0], uni_view[2][0]);
	vec3 CameraUp_worldspace = vec3 (uni_view[0][1], uni_view[1][1], uni_view[2][1]);

	vec3 c = vec3 (in_center.x, in_center.y, position.z);
	vec3 pos = position - c;
	vec3 vertexPosition_worldspace =
		c
		+ CameraRight_worldspace * pos.x + CameraUp_worldspace * pos.z;

	//vertexPosition_worldspace.z = position.z;
	//vertexPosition_worldspace.y += in_center.y;
	//vertexPosition_worldspace.x += in_center.x;

	vec4 snapToPixel = uni_projection * uni_view * vec4(vertexPosition_worldspace, 1.0);
	vec4 vertex = snapToPixel;

	//vertex.xyz = snapToPixel.xyz / snapToPixel.w;
	//vertex.x = floor(160 * vertex.x) / 160;
	//vertex.y = floor(120 * vertex.y) / 120;
	//vertex.xyz = vertex.xyz * snapToPixel.w;



    gl_Position = vertex;


    uv = in_uv;
	color = in_color;
}
