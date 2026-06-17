// Shared colour-grade post-process applied as the final step in every pixel shader.
// mode: 0=none, 1=warm, 2=greyscale, 3=nes_colors
float4 ApplyColorGrade(float4 c, float mode)
{
    if (mode < 0.5)
        return c;  // none — fast path

    if (mode < 1.5)
    {
        // warm — slight amber tint
        return float4(
            saturate(c.r * 1.07 + 0.015),
            saturate(c.g * 1.01),
            saturate(c.b * 0.85),
            c.a);
    }

    if (mode < 2.5)
    {
        // greyscale — BT.601 luma weights
        float luma = dot(c.rgb, float3(0.299, 0.587, 0.114));
        return float4(luma, luma, luma, c.a);
    }

    // mode ~= 3 — NES colour correction (2C02 composite → sRGB approximation)
    return float4(
        saturate( 1.04 * c.r + 0.00 * c.g - 0.04 * c.b),
        saturate( 0.00 * c.r + 1.00 * c.g + 0.00 * c.b),
        saturate(-0.08 * c.r + 0.04 * c.g + 0.96 * c.b),
        c.a);
}
