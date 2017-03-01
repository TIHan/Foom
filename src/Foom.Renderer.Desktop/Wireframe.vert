#version 330

in vec3 position;

uniform mat4x4 uni_projection;
uniform mat4x4 uni_view;

void main ()
{
    gl_Position = uni_projection * uni_view * vec4(position, 1.0);
}
