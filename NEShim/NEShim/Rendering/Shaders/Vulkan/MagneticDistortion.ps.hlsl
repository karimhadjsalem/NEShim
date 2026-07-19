// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../Dx11/ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    nesTexture;
[[vk::binding(0, 2)]] SamplerState nesSampler;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float phase;
    float amplitude;
    float frequency;
    float colorMode;
}

// Vertex stage is SDL's own built-in shader (SDL_GPURenderState swaps only the fragment
// shader): COLOR0 (vec4) at location 0, TEXCOORD0 (vec2) at location 1 — must match exactly.
struct PSInput
{
    float4 color    : COLOR0;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    float wave    = amplitude * sin(input.texcoord.y * frequency + phase);
    float2 warped = input.texcoord + float2(wave, 0.0);

    if (warped.x < 0.0 || warped.x > 1.0)
        return float4(0.0, 0.0, 0.0, 1.0) * input.color;

    float4 sampled = nesTexture.Sample(nesSampler, warped);
    return ApplyColorGrade(sampled, colorMode) * input.color;
}
