#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)  // fixed 4 floats: [0..2] filter params, [3] colorMode
{
    float nesWidth;           // NES content width in pixels
    float nesHeight;          // NES content height in pixels
    float scanlineIntensity;  // Brightness multiplier for darkened scanlines — alternating rows; 0=black gaps, 1=no gaps
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

    // Gaussian scanline profile: each NES scanline peaks at its centre and fades
    // toward the row edges, simulating the electron-beam spot on CRT phosphor.
    // k=8 gives FWHM ≈ 59% of row height — typical consumer CRT focus.
    float scanPos  = frac(input.texcoord.y * nesHeight);
    float gaussian = exp(-8.0 * (scanPos - 0.5) * (scanPos - 0.5));
    c.rgb *= lerp(scanlineIntensity, 1.0, gaussian);

    // Aperture grille: divide each NES pixel into 3 vertical sub-columns (R / G / B dominant).
    // Works correctly at 3× scale and above (typical at 1080p with a 256-wide NES frame).
    float col3 = fmod(floor(input.texcoord.x * nesWidth * 3.0), 3.0);
    float3 mask = col3 < 1.0 ? float3(1.0, 0.5, 0.5)
                : col3 < 2.0 ? float3(0.5, 1.0, 0.5)
                :              float3(0.5, 0.5, 1.0);
    c.rgb *= mask;

    return ApplyColorGrade(c, colorMode);
}
