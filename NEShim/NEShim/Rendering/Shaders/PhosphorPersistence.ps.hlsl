#include "ColorGrade.hlsli"

Texture2D    currentFrame : register(t0);   // current rendered NES frame
Texture2D    historyFrame : register(t1);   // previous output (phosphor persistence)
SamplerState frameSampler : register(s0);

cbuffer FilterParams : register(b0)  // fixed 4 floats: [0..2] filter params, [3] colorMode
{
    float decay;      // fraction of previous frame retained each tick (e.g. 0.65)
    float _unused0;
    float _unused1;
    float colorMode;
}

struct PSInput
{
    float4 pos      : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    float4 current  = currentFrame.Sample(frameSampler, input.texcoord);
    float4 previous = historyFrame.Sample(frameSampler, input.texcoord);

    // Take the brighter of the current frame and the decayed history.
    // max() instead of addition prevents overbright accumulation: additive blending
    // saturates any pixel above ~35% at steady state when decay=0.65, washing out
    // the whole image.  max() correctly represents "whichever phosphor is still
    // glowing" without compounding brightness.
    float4 result = max(current, previous * decay);
    return ApplyColorGrade(result, colorMode);
}
