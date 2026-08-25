#version 330 core
in vec2 vUV;

out vec4 FragColor;

uniform sampler2D uFrameTexture;

void main()
{
    FragColor = texture(uFrameTexture, vUV);
}
