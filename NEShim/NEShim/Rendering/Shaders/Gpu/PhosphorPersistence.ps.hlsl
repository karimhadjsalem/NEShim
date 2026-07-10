// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler (currentFrame + frameSampler)
//   set=2, binding=1 → combined image sampler (historyFrame + frameSampler)
//   set=3, binding=0 → uniform buffer (FilterParams)
// NOTE: This shader requires two texture bindings and cannot be used with
// SDL_CreateGPURenderState (which provides only one SDL renderer texture).
// It requires a full SDL_GPU graphics pipeline with two sampler inputs.
// NumSamplers=2 in SDL_GPUShaderCreateInfo.
#include "../ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    currentFrame;
[[vk::binding(0, 2)]] SamplerState frameSampler;
[[vk::binding(1, 2)]] Texture2D    historyFrame;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float decay;
    float _unused0;
    float _unused1;
    float colorMode;
}

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

float4 main(PSInput input) : SV_TARGET
{
    float4 current  = currentFrame.Sample(frameSampler, input.texcoord);
    float4 previous = historyFrame.Sample(frameSampler, input.texcoord);
    float4 result   = max(current, previous * decay);
    return ApplyColorGrade(result, colorMode);
}
