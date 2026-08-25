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
uniform float uBrightness;
uniform float uContrast;
uniform float uSaturation;
uniform float uSharpness;
uniform float uOutline;

vec4 sampleAdjustedColor(vec2 uv)
{
    vec2 texelSize = 1.0 / vec2(textureSize(uFrameTexture, 0));
    vec4 center = texture(uFrameTexture, uv);

    vec3 left = texture(uFrameTexture, clamp(uv + vec2(-texelSize.x, 0.0), vec2(0.0), vec2(1.0))).rgb;
    vec3 right = texture(uFrameTexture, clamp(uv + vec2(texelSize.x, 0.0), vec2(0.0), vec2(1.0))).rgb;
    vec3 up = texture(uFrameTexture, clamp(uv + vec2(0.0, -texelSize.y), vec2(0.0), vec2(1.0))).rgb;
    vec3 down = texture(uFrameTexture, clamp(uv + vec2(0.0, texelSize.y), vec2(0.0), vec2(1.0))).rgb;

    vec3 edgeDetail = center.rgb * 4.0 - left - right - up - down;
    float centerGray = dot(center.rgb, vec3(0.299, 0.587, 0.114));
    float edge =
        abs(centerGray - dot(left, vec3(0.299, 0.587, 0.114))) +
        abs(centerGray - dot(right, vec3(0.299, 0.587, 0.114))) +
        abs(centerGray - dot(up, vec3(0.299, 0.587, 0.114))) +
        abs(centerGray - dot(down, vec3(0.299, 0.587, 0.114)));

    vec3 rgb = center.rgb + edgeDetail * uSharpness * 0.25;

    rgb = (rgb - vec3(0.5)) * uContrast + vec3(0.5);
    float adjustedGray = dot(rgb, vec3(0.299, 0.587, 0.114));
    rgb = mix(vec3(adjustedGray), rgb, uSaturation);
    rgb += vec3(uBrightness);
    float outlineMask = smoothstep(0.12, 0.35, edge * uOutline * 5.0);
    rgb = mix(rgb, vec3(0.0), outlineMask * uOutline);

    return vec4(clamp(rgb, vec3(0.0), vec3(1.0)), center.a);
}

void main()
{
    vec2 screenDelta = (vLocalPos - uMagnifierCenterLocal) * uScale;
    screenDelta.x *= uViewportAspect;
    float lensDistance = length(screenDelta);

    vec2 magnifiedUv = uMagnifierCenterUv + (vUV - uMagnifierCenterUv) / uMagnification;
    magnifiedUv = clamp(magnifiedUv, vec2(0.0), vec2(1.0));

    vec4 normalColor = sampleAdjustedColor(vUV);
    vec4 magnifiedColor = sampleAdjustedColor(magnifiedUv);

    float feather = 0.025;
    float lensMask = 1.0 - smoothstep(uMagnifierRadius - feather, uMagnifierRadius, lensDistance);
    lensMask *= float(uMagnifierEnabled);

    vec4 color = mix(normalColor, magnifiedColor, lensMask);

    FragColor = color;
}
