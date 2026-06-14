Texture2D    nesTexture : register(t0);
SamplerState nesSampler : register(s0);

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    return nesTexture.Sample(nesSampler, input.texcoord);
}
