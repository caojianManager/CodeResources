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
    vec2 sampleUv = vUV;
    vec2 screenDelta = (vLocalPos - uMagnifierCenterLocal) * uScale;
    screenDelta.x *= uViewportAspect;
    float lensDistance = length(screenDelta);

    if (uMagnifierEnabled == 1 && lensDistance < uMagnifierRadius)
    {
        sampleUv = uMagnifierCenterUv + (vUV - uMagnifierCenterUv) / uMagnification;
        sampleUv = clamp(sampleUv, vec2(0.0), vec2(1.0));
    }

    vec4 color = texture(uFrameTexture, sampleUv);

    if (uMagnifierEnabled == 1)
    {
        float border = smoothstep(uMagnifierRadius, uMagnifierRadius - 0.012, lensDistance);
        float outer = smoothstep(uMagnifierRadius + 0.012, uMagnifierRadius, lensDistance);
        float ring = outer * (1.0 - border);
        color.rgb = mix(color.rgb, vec3(1.0, 0.95, 0.25), ring * 0.85);
    }

    FragColor = color;
}
