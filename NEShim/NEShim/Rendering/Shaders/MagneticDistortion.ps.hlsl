#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)  // fixed 4 floats: [0..2] filter params, [3] colorMode
{
    float phase;      // Wave phase in radians — incremented each frame by MagneticDistortionMotionEffect
    float amplitude;  // Maximum horizontal UV displacement (~0.012–0.018)
    float frequency;  // Spatial cycles of the wave per frame height in UV space
    float colorMode;
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    // Displace each row horizontally by a sine wave whose phase shifts over time.
    // Adjacent rows land on opposite sides of the wave, producing the characteristic
    // non-uniform warp of CRT magnetic interference.
    float wave   = amplitude * sin(input.texcoord.y * frequency + phase);
    float2 warped = input.texcoord + float2(wave, 0.0);

    // Pixels that warp past the horizontal texture boundary render as black,
    // matching the edge roll-off seen on real CRTs under magnetic influence.
    if (warped.x < 0.0 || warped.x > 1.0)
        return float4(0.0, 0.0, 0.0, 1.0);

    float4 color = nesTexture.Sample(nesSampler, warped);
    return ApplyColorGrade(color, colorMode);
}
