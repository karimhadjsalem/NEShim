// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler (nesTexture + nesSampler)
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    nesTexture;
[[vk::binding(0, 2)]] SamplerState nesSampler;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float param0;
    float param1;
    float param2;
    float colorMode;
}

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

float4 main(PSInput input) : SV_TARGET
{
    float4 c = nesTexture.Sample(nesSampler, input.texcoord);
    return ApplyColorGrade(c, colorMode);
}
