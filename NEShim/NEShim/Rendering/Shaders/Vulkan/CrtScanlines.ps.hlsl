// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../Dx11/ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    nesTexture;
[[vk::binding(0, 2)]] SamplerState nesSampler;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float nesWidth;
    float nesHeight;
    float scanlineIntensity;
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
    float4 c = nesTexture.Sample(nesSampler, input.texcoord);

    float scanPos  = frac(input.texcoord.y * nesHeight);
    float gaussian = exp(-8.0 * (scanPos - 0.5) * (scanPos - 0.5));
    c.rgb *= lerp(scanlineIntensity, 1.0, gaussian);

    return ApplyColorGrade(c, colorMode) * input.color;
}
