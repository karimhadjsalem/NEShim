// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../Dx11/ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    nesTexture;
[[vk::binding(0, 2)]] SamplerState nesSampler;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float barrelStrength;
    float chromaStrength;
    float vignetteStrength;
    float colorMode;
}

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

float2 BarrelWarp(float2 uv, float k)
{
    float2 c = uv - 0.5;
    float r2 = dot(c, c);
    return c * (1.0 + k * r2) + 0.5;
}

float4 main(PSInput input) : SV_TARGET
{
    float2 uv = input.texcoord;

    float2 uvR = BarrelWarp(uv, barrelStrength + chromaStrength);
    float2 uvG = BarrelWarp(uv, barrelStrength);
    float2 uvB = BarrelWarp(uv, barrelStrength - chromaStrength);

    if (any(uvG < 0.0) || any(uvG > 1.0))
        return float4(0.0, 0.0, 0.0, 1.0);

    float r = nesTexture.Sample(nesSampler, uvR).r;
    float g = nesTexture.Sample(nesSampler, uvG).g;
    float b = nesTexture.Sample(nesSampler, uvB).b;
    float4 c = float4(r, g, b, 1.0);

    float2 vc  = uv - 0.5;
    float vign = 1.0 - vignetteStrength * dot(vc, vc) * 4.0;
    c.rgb *= saturate(vign);

    return ApplyColorGrade(c, colorMode);
}
