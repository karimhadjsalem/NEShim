#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)
{
    float nesWidth;           // NES content width in pixels
    float nesHeight;          // NES content height in pixels
    float scanlineIntensity;  // Darkening factor for even scanlines (0=black, 1=no effect)
    float colorMode;          // 0=none, 1=warm, 2=greyscale, 3=nes_colors, 4=cool
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    float4 c = nesTexture.Sample(nesSampler, input.texcoord);

    // Darken every other row to simulate a CRT scanline gap.
    float row  = floor(input.texcoord.y * nesHeight);
    float dark = fmod(row, 2.0);
    c.rgb = c.rgb * lerp(1.0, scanlineIntensity, dark);

    // Aperture grille: divide each NES pixel into 3 vertical sub-columns (R / G / B dominant).
    // Works correctly at 3× scale and above (typical at 1080p with a 256-wide NES frame).
    float col3 = fmod(floor(input.texcoord.x * nesWidth * 3.0), 3.0);
    float3 mask = col3 < 1.0 ? float3(1.0, 0.5, 0.5)
                : col3 < 2.0 ? float3(0.5, 1.0, 0.5)
                :              float3(0.5, 0.5, 1.0);
    c.rgb *= mask;

    return ApplyColorGrade(c, colorMode);
}
