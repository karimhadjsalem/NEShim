#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)
{
    float param0;     // unused for passthrough
    float param1;     // unused for passthrough
    float param2;     // unused for passthrough
    float colorMode;  // 0=none, 1=warm, 2=greyscale, 3=nes_colors
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    float4 c = nesTexture.Sample(nesSampler, input.texcoord);
    return ApplyColorGrade(c, colorMode);
}
