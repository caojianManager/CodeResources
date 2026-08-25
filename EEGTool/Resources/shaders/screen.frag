#version 330 core
in vec3 FragPos;
in vec3 Normal;
in vec3 vUV;

out vec4 FragColor;

uniform vec3 lightPos;
uniform vec3 viewPos;
uniform vec3 objectColor;
uniform vec3 lightColor;


uniform int uNumElectrodes;             
uniform vec3 uElectrodePositions[32];  
                                         
uniform float uPower = 2.0;           


float computeWeightedValue(vec2 uv)
{
    float sumWeight = 0.0;
    float sumValue = 0.0;
    
    for(int i = 0; i < uNumElectrodes; i++)
    {
        vec2 ePos = uElectrodePositions[i].xy;
        float val = uElectrodePositions[i].z;
        
        float d = distance(uv, ePos);
        d = max(d, 0.001);
        
        float w = 1.0 / pow(d, uPower);
        
        sumValue += val * w;
        sumWeight += w;
    }
    
    return sumValue / sumWeight;
}

vec3 colormap(float val)
{
    float r = clamp(val, 0.0, 1.0);
    float b = clamp(-val, 0.0, 1.0);
    float g = 1.0 - r - b; 
    return vec3(r, g, b);
}


void main()
{

    float val = computeWeightedValue(vec2(vUV.x,vUV.y));
    vec3 heatColor = colormap(val);

    float ambientStrength = 0.2;
    vec3 ambient = ambientStrength * lightColor;

    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * lightColor;

    vec3 result = (ambient + diffuse) * objectColor;
    FragColor = vec4(result, 1.0);
}


