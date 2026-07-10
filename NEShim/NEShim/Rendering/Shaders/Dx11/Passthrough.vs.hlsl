struct VSInput
{
    float2 pos      : POSITION;
    float2 texcoord : TEXCOORD0;
};

struct VSOutput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.pos      = float4(input.pos, 0.0f, 1.0f);
    output.texcoord = input.texcoord;
    return output;
}
