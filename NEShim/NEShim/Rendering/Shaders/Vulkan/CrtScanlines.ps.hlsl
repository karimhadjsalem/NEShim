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

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

float4 main(PSInput input) : SV_TARGET
{
    float4 c = nesTexture.Sample(nesSampler, input.texcoord);

    float scanPos  = frac(input.texcoord.y * nesHeight);
    float gaussian = exp(-8.0 * (scanPos - 0.5) * (scanPos - 0.5));
    c.rgb *= lerp(scanlineIntensity, 1.0, gaussian);

    return ApplyColorGrade(c, colorMode);
}
