// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    nesTexture;
[[vk::binding(0, 2)]] SamplerState nesSampler;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float phase;
    float amplitude;
    float frequency;
    float colorMode;
}

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

float4 main(PSInput input) : SV_TARGET
{
    float wave    = amplitude * sin(input.texcoord.y * frequency + phase);
    float2 warped = input.texcoord + float2(wave, 0.0);

    if (warped.x < 0.0 || warped.x > 1.0)
        return float4(0.0, 0.0, 0.0, 1.0);

    float4 color = nesTexture.Sample(nesSampler, warped);
    return ApplyColorGrade(color, colorMode);
}
