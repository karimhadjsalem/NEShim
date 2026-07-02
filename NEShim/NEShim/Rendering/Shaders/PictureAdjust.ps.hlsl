// Post-process pixel shader for Brightness / Contrast / Saturation adjustments.
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
    float _pad;
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    float4 c = inputTexture.Sample(inputSampler, input.texcoord);

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
