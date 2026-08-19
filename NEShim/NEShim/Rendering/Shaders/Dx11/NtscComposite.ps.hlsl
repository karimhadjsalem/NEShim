#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)  // fixed 4 floats: [0..2] filter params, [3] colorMode
{
    float invWidth;     // 1 / nesWidth
    float frameParity;  // 0.0 or 1.0, alternating per draw frame — animates the noise grain
    float chromaStrength; // Chroma blur amount and cross-talk intensity (0=none, 1=full)
    float colorMode;    // 0=none, 1=warm, 2=greyscale, 3=nes_colors, 4=cool
}

struct PSInput { float4 pos : SV_POSITION; float2 texcoord : TEXCOORD0; };

// Converts linear RGB to YIQ (NTSC colour space).
// Y = luma, I = orange-cyan axis, Q = purple-green axis.
float3 RGBtoYIQ(float3 rgb)
{
    return float3(
        dot(rgb, float3( 0.299,  0.587,  0.114)),
        dot(rgb, float3( 0.596, -0.274, -0.322)),
        dot(rgb, float3( 0.211, -0.523,  0.312)));
}

// Converts YIQ back to linear RGB.
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

    // Sample five horizontally-adjacent pixels and convert each to YIQ.
    // All five pixels contribute to the chroma decode window; luma uses the centre only.
    float3 yl2 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x - 2.0*invWidth, input.texcoord.y)).rgb);
    float3 yl1 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x -     invWidth, input.texcoord.y)).rgb);
    float3 yc  = RGBtoYIQ(center.rgb);
    float3 yr1 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x +     invWidth, input.texcoord.y)).rgb);
    float3 yr2 = RGBtoYIQ(nesTexture.Sample(nesSampler, float2(input.texcoord.x + 2.0*invWidth, input.texcoord.y)).rgb);

    // Luma: use the centre pixel (NTSC luma bandwidth ~3.2 MHz, close to the pixel rate).
    float Y = yc.x;

    // Chroma IQ: 5-tap Gaussian-weighted average (sigma ~1 pixel).
    // NTSC chroma bandwidth ~1 MHz — roughly 3x narrower than luma — so chroma
    // blurs roughly 3 pixels sideways. This is what causes the characteristic colour
    // smearing on high-contrast hue boundaries.
    float2 IQ_blurred = yl2.yz * 0.0625 + yl1.yz * 0.25 + yc.yz * 0.375
                      + yr1.yz * 0.25   + yr2.yz * 0.0625;
    float2 IQ = lerp(yc.yz, IQ_blurred, chromaStrength);

    // Luma-chroma cross-talk: energy at colour-boundary transitions leaks into luma.
    float chromaEnergy = (abs(yl1.y - yr1.y) + abs(yl1.z - yr1.z)) * chromaStrength * 0.08;
    Y = saturate(Y + chromaEnergy);

    // Reconstruct RGB and add a temporally animated noise layer (analogue grain).
    // frameParity alternates 0/1 each draw call, shifting the noise seed so the grain
    // moves every frame — producing the shimmer characteristic of a real composite signal.
    float3 rgb   = saturate(YIQtoRGB(float3(Y, IQ)));
    float  noise = frac(sin(dot(input.texcoord + frameParity * 0.0057, float2(127.1, 311.7))) * 43758.5453) * 0.04;
    rgb = saturate(rgb + noise);

    return ApplyColorGrade(float4(rgb, center.a), colorMode);
}
