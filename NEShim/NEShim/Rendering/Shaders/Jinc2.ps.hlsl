#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)
{
    float nesWidth;
    float nesHeight;
    float _pad;
    float colorMode;
};

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

float sinc_norm(float x)
{
    float xp = x * 3.14159265358979;
    return (abs(xp) > 1e-5) ? sin(xp) / xp : 1.0;
}

float jinc2_kernel(float r)
{
    const float r1    = 1.2197;
    const float r_max = 2.0 * r1;
    float hann = 0.5 + 0.5 * cos(3.14159265358979 * r / r_max);
    return (1.0 - step(r_max, r)) * sinc_norm(r / r1) * hann;
}

float4 main(PSInput input) : SV_TARGET
{
    float2 dims     = float2(nesWidth, nesHeight);
    float2 texelPos = input.texcoord * dims - 0.5;
    float2 base     = floor(texelPos);
    float2 frac     = texelPos - base;

    float4 result      = 0;
    float  totalWeight = 0;

    [unroll]
    for (int dy = -1; dy <= 2; dy++)
    {
        [unroll]
        for (int dx = -1; dx <= 2; dx++)
        {
            float2 offset = float2(dx, dy) - frac;
            float  dist   = length(offset);
            float  w      = jinc2_kernel(dist);

            float2 sampleUv = (base + float2(dx, dy) + 0.5) / dims;
            result      += w * nesTexture.Sample(nesSampler, sampleUv);
            totalWeight += w;
        }
    }

    return ApplyColorGrade(result / totalWeight, colorMode);
}
