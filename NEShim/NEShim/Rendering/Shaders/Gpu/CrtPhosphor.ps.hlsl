// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../ColorGrade.hlsli"

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

    float col3 = fmod(floor(input.texcoord.x * nesWidth * 3.0), 3.0);
    float3 mask = col3 < 1.0 ? float3(1.0, 0.5, 0.5)
                : col3 < 2.0 ? float3(0.5, 1.0, 0.5)
                :              float3(0.5, 0.5, 1.0);
    c.rgb *= mask;

    return ApplyColorGrade(c, colorMode);
}
