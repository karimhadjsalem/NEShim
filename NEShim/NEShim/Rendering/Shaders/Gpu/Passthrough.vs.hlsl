// SDL_GPU vertex shader — full GPU pipeline (not needed for SDL_CreateGPURenderState).
// SDL_GPU requires non-system-value semantics to use TEXCOORD prefix.
struct VSInput
{
    float2 pos      : TEXCOORD0;
    float2 texcoord : TEXCOORD1;
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
