#version 330 core

in vec3 position;
in vec2 in_uv;
in vec4 in_color;


in vec3 instance_position;
in vec4 instance_lightLevel;
in vec4 instance_uvOffset;

uniform mat4x4 uni_projection;
uniform mat4x4 uni_view;
uniform vec2 uTextureResolution;

out vec2 uv;
out vec4 color;
out vec4 lightLevel;

void main ()
{
	float offsetX = instance_uvOffset.x;
	float offsetY = instance_uvOffset.y;
	float width = instance_uvOffset.z;
	float height = instance_uvOffset.w;

	float halfX = width / 2.0;
	vec3 min = vec3 (-halfX, 0, 0);
	vec3 max = vec3 (halfX, 0, height);
	vec3 mid = min + ((max - min) / 2.0);

	vec3 pos0 = position * vec3 (halfX, 0, height);

	vec3 CameraRight_worldspace = vec3 (uni_view[0][0], uni_view[1][0], uni_view[2][0]);
	vec3 CameraUp_worldspace = vec3 (uni_view[0][1], uni_view[1][1], uni_view[2][1]);

	vec3 c = vec3 (mid.x, 0, pos0.z) + instance_position;
	vec3 pos = pos0 - c + instance_position;
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


    float uvX = (width / uTextureResolution.x) * in_uv.x + (offsetX / uTextureResolution.x);
    float uvY = (height / uTextureResolution.y) * in_uv.y + (offsetY / uTextureResolution.y);

    uv = vec2 (uvX, uvY - (1.0 - height / uTextureResolution.y));
	color = in_color;
	lightLevel = instance_lightLevel;
}
