// Post-process pixel shader for Brightness / Contrast / Saturation / Hue adjustments.
// Applied as a full-viewport pass after all structural/motion filter rendering
// and before the overlay (menus, HUD).  Does NOT include ColorGrade.hlsli because
// colour grading is already baked into the input from the upstream structural filter.

Texture2D    inputTexture : register(t0);
SamplerState inputSampler : register(s0);

cbuffer AdjustParams : register(b0)  // independent b0 layout, no colorMode slot
{
    float brightness;  // additive shift: 0 = neutral  (e.g. -0.2 .. +0.2)
    float contrast;    // scale factor:  1 = neutral  (e.g.  0.5 .. 2.0)
    float saturation;  // saturation:    1 = neutral  (e.g.  0.0 .. 2.0)
    float hue;         // hue angle in radians: 0 = neutral  (e.g. -3.14 .. +3.14)
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

// Rotates RGB hue by angle radians using Rodrigues' rotation around the grey axis.
// k = 1/3, sq = sqrt(1/3).  The rotation matrix entries collapse to three unique values:
//   a = cos + k*(1-cos),  b = k*(1-cos) + sq*sin,  c = k*(1-cos) - sq*sin
float3 rotateHue(float3 col, float angle)
{
    float cosA = cos(angle);
    float sinA = sin(angle);
    float k    = 1.0 / 3.0;
    float sq   = 0.57735026919;  // sqrt(1/3)
    float a = cosA + k * (1.0 - cosA);
    float b = k * (1.0 - cosA) + sq * sinA;
    float c = k * (1.0 - cosA) - sq * sinA;
    float3x3 M = float3x3(a, c, b,
                           b, a, c,
                           c, b, a);
    return saturate(mul(M, col));
}

float4 main(PSInput input) : SV_TARGET
{
    float4 c = inputTexture.Sample(inputSampler, input.texcoord);

    // Hue rotation (applied first, before saturation/contrast/brightness).
    if (hue != 0.0)
        c.rgb = rotateHue(c.rgb, hue);

    // Saturation: linearly blend each channel toward its own luminance.
    float luma = dot(c.rgb, float3(0.299, 0.587, 0.114));
    c.rgb = lerp(float3(luma, luma, luma), c.rgb, saturation);

    // Contrast: scale around mid-grey (0.5).
    c.rgb = (c.rgb - 0.5) * contrast + 0.5;

    // Brightness: additive offset.
    c.rgb += brightness;

    c.rgb = saturate(c.rgb);
    return float4(c.rgb, c.a);
}
