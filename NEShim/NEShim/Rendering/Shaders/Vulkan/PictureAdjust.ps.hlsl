// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (AdjustParams)
// Note: does not include ColorGrade.hlsli — color grading is already applied upstream.

[[vk::binding(0, 2)]] Texture2D    inputTexture;
[[vk::binding(0, 2)]] SamplerState inputSampler;

[[vk::binding(0, 3)]] cbuffer AdjustParams
{
    float brightness;
    float contrast;
    float saturation;
    float hue;
}

// Vertex stage is SDL's own built-in shader (SDL_GPURenderState swaps only the fragment
// shader): COLOR0 (vec4) at location 0, TEXCOORD0 (vec2) at location 1 — must match exactly.
struct PSInput
{
    float4 color    : COLOR0;
    float2 texcoord : TEXCOORD0;
};

float3 rotateHue(float3 col, float angle)
{
    float cosA = cos(angle);
    float sinA = sin(angle);
    float k    = 1.0 / 3.0;
    float sq   = 0.57735026919;
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

    if (hue != 0.0)
        c.rgb = rotateHue(c.rgb, hue);

    float luma = dot(c.rgb, float3(0.299, 0.587, 0.114));
    c.rgb = lerp(float3(luma, luma, luma), c.rgb, saturation);
    c.rgb = (c.rgb - 0.5) * contrast + 0.5;
    c.rgb += brightness;
    c.rgb = saturate(c.rgb);
    return float4(c.rgb, c.a) * input.color;
}
