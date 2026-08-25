#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUV;

out vec2 vUV;
out vec2 vLocalPos;

uniform vec2 uScale;
uniform vec2 uOffset;

void main()
{
    vUV = aUV;
    vLocalPos = aPos;
    gl_Position = vec4(aPos * uScale + uOffset, 0.0, 1.0);
}
