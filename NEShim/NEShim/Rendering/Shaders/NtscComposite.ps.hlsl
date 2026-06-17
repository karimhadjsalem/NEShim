#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)
{
    float invWidth;       // 1 / nesWidth
    float invHeight;      // 1 / nesHeight (unused, kept for alignment)
    float chromaStrength; // Blending strength for chroma smear (0=none, 1=full)
    float colorMode;      // 0=none, 1=warm, 2=greyscale, 3=nes_colors
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    float4 c  = nesTexture.Sample(nesSampler, input.texcoord);
    float4 c1 = nesTexture.Sample(nesSampler, float2(input.texcoord.x - invWidth, input.texcoord.y));
    float4 c2 = nesTexture.Sample(nesSampler, float2(input.texcoord.x + invWidth, input.texcoord.y));

    // Subtle grain
    float noise = frac(sin(dot(input.texcoord, float2(127.1, 311.7))) * 43758.5453) * 0.025;

    // Horizontal chroma smear: red and blue bleed sideways, green holds steady
    float cr = lerp(c.r, (c1.r + c.r + c2.r) / 3.0, chromaStrength * 0.6);
    float cb = lerp(c.b, (c1.b + c.b + c2.b) / 3.0, chromaStrength);

    float3 ntsc = float3(
        saturate(cr + noise),
        saturate(c.g * 0.98 + noise * 0.5),
        saturate(cb));

    float4 result = float4(lerp(c.rgb, ntsc, chromaStrength), c.a);
    return ApplyColorGrade(result, colorMode);
}
