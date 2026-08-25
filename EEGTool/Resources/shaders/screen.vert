#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUV;

out vec2 vUV;

uniform vec2 uScale;

void main()
{
    vUV = aUV;
    gl_Position = vec4(aPos * uScale, 0.0, 1.0);
}
