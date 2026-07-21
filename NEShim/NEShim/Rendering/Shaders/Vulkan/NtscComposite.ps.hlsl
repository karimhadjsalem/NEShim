// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../Dx11/ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    nesTexture;
[[vk::binding(0, 2)]] SamplerState nesSampler;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float invWidth;
    float frameParity;
    float chromaStrength;
    float colorMode;
}

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

float3 RGBtoYIQ(float3 rgb)
{
    return float3(
        dot(rgb, float3( 0.299,  0.587,  0.114)),
        dot(rgb, float3( 0.596, -0.274, -0.322)),
        dot(rgb, float3( 0.211, -0.523,  0.312)));
}

float3 YIQtoRGB(float3 yiq)
{
    return float3(
        dot(yiq, float3(1.0,  0.956,  0.621)),
        dot(yiq, float3(1.0, -0.272, -0.647)),
        dot(yiq, float3(1.0, -1.106,  1.703)));
}

float4 main(PSInput input) : SV_TARGET
{
    float4 center = nesTexture.Sample(nesSampler, input.texcoord);

    float3 yl2 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x - 2.0*invWidth, input.texcoord.y)).rgb);
    float3 yl1 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x -     invWidth, input.texcoord.y)).rgb);
    float3 yc  = RGBtoYIQ(center.rgb);
    float3 yr1 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x +     invWidth, input.texcoord.y)).rgb);
    float3 yr2 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x + 2.0*invWidth, input.texcoord.y)).rgb);

    float Y = yc.x;
    float2 IQ_blurred = yl2.yz * 0.0625 + yl1.yz * 0.25 + yc.yz * 0.375
                      + yr1.yz * 0.25   + yr2.yz * 0.0625;
    float2 IQ = lerp(yc.yz, IQ_blurred, chromaStrength);

    float chromaEnergy = (abs(yl1.y - yr1.y) + abs(yl1.z - yr1.z)) * chromaStrength * 0.08;
    Y = saturate(Y + chromaEnergy);

    float3 rgb   = saturate(YIQtoRGB(float3(Y, IQ)));
    float  noise = frac(sin(dot(input.texcoord + frameParity * 0.0057, float2(127.1, 311.7))) * 43758.5453) * 0.04;
    rgb = saturate(rgb + noise);

    return ApplyColorGrade(float4(rgb, center.a), colorMode);
}
