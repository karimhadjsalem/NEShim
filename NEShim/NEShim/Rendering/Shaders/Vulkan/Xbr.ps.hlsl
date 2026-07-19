// SDL_GPU binding layout — fragment stage:
//   set=2, binding=0 → combined image sampler
//   set=3, binding=0 → uniform buffer (FilterParams)
#include "../Dx11/ColorGrade.hlsli"

[[vk::binding(0, 2)]] Texture2D    nesTexture;
[[vk::binding(0, 2)]] SamplerState nesSampler;

[[vk::binding(0, 3)]] cbuffer FilterParams
{
    float nesWidth;
    float nesHeight;
    float _unused;
    float colorMode;
}

// Vertex stage is SDL's own built-in shader (SDL_GPURenderState swaps only the fragment
// shader): COLOR0 (vec4) at location 0, TEXCOORD0 (vec2) at location 1 — must match exactly.
struct PSInput
{
    float4 color    : COLOR0;
    float2 texcoord : TEXCOORD0;
};

bool Similar(float4 a, float4 b)
{
    float3 d = abs(a.rgb - b.rgb);
    return max(max(d.r, d.g), d.b) < (1.5 / 255.0);
}

float4 main(PSInput input) : SV_TARGET
{
    float2 texelSize = float2(1.0 / nesWidth, 1.0 / nesHeight);
    float2 nesCoord  = input.texcoord * float2(nesWidth, nesHeight);
    float2 biasedCoord = nesCoord + 1e-4;
    float2 sub         = frac(biasedCoord);
    float2 pUV = (floor(biasedCoord) + 0.5) * texelSize;

    float4 P = nesTexture.Sample(nesSampler, pUV);
    float4 N = nesTexture.Sample(nesSampler, pUV + float2(0,            -texelSize.y));
    float4 W = nesTexture.Sample(nesSampler, pUV + float2(-texelSize.x,  0));
    float4 E = nesTexture.Sample(nesSampler, pUV + float2( texelSize.x,  0));
    float4 S = nesTexture.Sample(nesSampler, pUV + float2(0,             texelSize.y));

    float4 result;
    if (sub.x < 0.5 && sub.y < 0.5)
        result = (Similar(W,N) && !Similar(W,S) && !Similar(N,E)) ? N : P;
    else if (sub.x >= 0.5 && sub.y < 0.5)
        result = (Similar(N,E) && !Similar(N,W) && !Similar(E,S)) ? E : P;
    else if (sub.x < 0.5)
        result = (Similar(S,W) && !Similar(S,E) && !Similar(W,N)) ? W : P;
    else
        result = (Similar(E,S) && !Similar(E,N) && !Similar(S,W)) ? S : P;

    return ApplyColorGrade(result, colorMode) * input.color;
}
