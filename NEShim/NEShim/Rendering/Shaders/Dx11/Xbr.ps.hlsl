#include "ColorGrade.hlsli"

Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

cbuffer FilterParams : register(b0)  // fixed 4 floats: [0..2] filter params, [3] colorMode
{
    float nesWidth;
    float nesHeight;
    float _unused;
    float colorMode;
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

// Two texels are "similar" when every RGB channel differs by less than 1.5 steps
// out of 255.  NES frames are palette-indexed; point-sampled texels carry exact
// palette values, so this is effectively exact-equality with a tiny float-
// precision margin.  Luminance-only comparison is wrong here: two palette entries
// can share the same luminance while having completely different hues (e.g. dark
// blue vs dark green), which would cause EPX to replace pixel-art edges with the
// wrong colour.
bool Similar(float4 a, float4 b)
{
    float3 d = abs(a.rgb - b.rgb);
    return max(max(d.r, d.g), d.b) < (1.5 / 255.0);
}

float4 main(PSInput input) : SV_TARGET
{
    float2 texelSize = float2(1.0 / nesWidth, 1.0 / nesHeight);

    // Fractional position within the NES texel grid.
    // sub.x < 0.5  → left  sub-pixel  |  sub.x >= 0.5 → right
    // sub.y < 0.5  → top   sub-pixel  |  sub.y >= 0.5 → bottom
    float2 nesCoord = input.texcoord * float2(nesWidth, nesHeight);

    // Bias by a tiny epsilon before floor/frac.  At certain non-integer display scales
    // (e.g. 4.5× = 1080/240 on a 1080p screen) the GPU interpolates the UV as
    // 0.9999... instead of exactly 1.0, making floor() return j–1 and assigning the
    // first output row of a texel to the previous texel's BL/BR quadrant.  The result
    // is one missing output row per affected NES row, visible as thin horizontal lines
    // appearing truncated.  The bias (1e-4) is large enough to clear the FP underflow
    // (~1e-7) but far smaller than the minimum inter-row sub step at any practical
    // display scale, so it never shifts a legitimately fractional sub value across the
    // 0.5 quadrant boundary.
    float2 biasedCoord = nesCoord + 1e-4;
    float2 sub         = frac(biasedCoord);

    // UV of the nearest NES texel centre (clamped sampler handles edges).
    float2 pUV = (floor(biasedCoord) + 0.5) * texelSize;

    float4 P = nesTexture.Sample(nesSampler, pUV);
    float4 N = nesTexture.Sample(nesSampler, pUV + float2(0,            -texelSize.y));
    float4 W = nesTexture.Sample(nesSampler, pUV + float2(-texelSize.x,  0));
    float4 E = nesTexture.Sample(nesSampler, pUV + float2( texelSize.x,  0));
    float4 S = nesTexture.Sample(nesSampler, pUV + float2(0,             texelSize.y));

    // Scale2x / EPX rule: each NES texel expands to a 2x2 block.
    // The quadrant colour is chosen to sharpen edges without blurring diagonals.
    //   TL = (W==N && W!=S && N!=E) ? N : P
    //   TR = (N==E && N!=W && E!=S) ? E : P
    //   BL = (S==W && S!=E && W!=N) ? W : P
    //   BR = (E==S && E!=N && S!=W) ? S : P
    float4 result;
    if (sub.x < 0.5 && sub.y < 0.5)
        result = (Similar(W,N) && !Similar(W,S) && !Similar(N,E)) ? N : P;
    else if (sub.x >= 0.5 && sub.y < 0.5)
        result = (Similar(N,E) && !Similar(N,W) && !Similar(E,S)) ? E : P;
    else if (sub.x < 0.5)
        result = (Similar(S,W) && !Similar(S,E) && !Similar(W,N)) ? W : P;
    else
        result = (Similar(E,S) && !Similar(E,N) && !Similar(S,W)) ? S : P;

    return ApplyColorGrade(result, colorMode);
}
