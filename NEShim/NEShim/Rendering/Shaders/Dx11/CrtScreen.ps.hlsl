#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)  // fixed 4 floats: [0..2] filter params, [3] colorMode
{
    float barrelStrength;   // k factor for barrel warp (positive = pincushion in UV space → curved corners)
    float chromaStrength;   // per-channel barrel delta for chromatic aberration
    float vignetteStrength; // 0=none, 1=full darkening at corners
    float colorMode;        // 0=none, 1=warm, 2=greyscale, 3=nes_colors, 4=cool, 5=phosphor_amber, 6=phosphor_green
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float2 BarrelWarp(float2 uv, float k)
{
    float2 c = uv - 0.5;
    float r2 = dot(c, c);
    return c * (1.0 + k * r2) + 0.5;
}

float4 main(PSInput input) : SV_TARGET
{
    float2 uv = input.texcoord;

    // Sample each channel with a slightly different barrel strength for chromatic aberration.
    float2 uvR = BarrelWarp(uv, barrelStrength + chromaStrength);
    float2 uvG = BarrelWarp(uv, barrelStrength);
    float2 uvB = BarrelWarp(uv, barrelStrength - chromaStrength);

    // Out-of-bounds UVs (beyond the curved corners) are black.
    if (any(uvG < 0.0) || any(uvG > 1.0))
        return float4(0.0, 0.0, 0.0, 1.0);

    float r = nesTexture.Sample(nesSampler, uvR).r;
    float g = nesTexture.Sample(nesSampler, uvG).g;
    float b = nesTexture.Sample(nesSampler, uvB).b;
    float4 c = float4(r, g, b, 1.0);

    // Vignette — quadratic falloff from center; scale so full-corner has the configured darkening.
    float2 vc  = uv - 0.5;
    float vign = 1.0 - vignetteStrength * dot(vc, vc) * 4.0;
    c.rgb *= saturate(vign);

    return ApplyColorGrade(c, colorMode);
}
