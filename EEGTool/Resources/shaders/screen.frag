#version 330 core
in vec2 vUV;
in vec2 vLocalPos;

out vec4 FragColor;

uniform sampler2D uFrameTexture;
uniform vec2 uScale;
uniform vec2 uMagnifierCenterUv;
uniform vec2 uMagnifierCenterLocal;
uniform float uMagnifierRadius;
uniform float uMagnification;
uniform int uMagnifierEnabled;
uniform float uViewportAspect;

void main()
{
    vec2 screenDelta = (vLocalPos - uMagnifierCenterLocal) * uScale;
    screenDelta.x *= uViewportAspect;
    float lensDistance = length(screenDelta);

    vec2 magnifiedUv = uMagnifierCenterUv + (vUV - uMagnifierCenterUv) / uMagnification;
    magnifiedUv = clamp(magnifiedUv, vec2(0.0), vec2(1.0));

    vec4 normalColor = texture(uFrameTexture, vUV);
    vec4 magnifiedColor = texture(uFrameTexture, magnifiedUv);

    float feather = 0.025;
    float lensMask = 1.0 - smoothstep(uMagnifierRadius - feather, uMagnifierRadius, lensDistance);
    lensMask *= float(uMagnifierEnabled);

    vec4 color = mix(normalColor, magnifiedColor, lensMask);

    FragColor = color;
}
