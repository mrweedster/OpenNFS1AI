// GammaCorrect.fx — MonoGame 3.8 / DesktopGL (SM 3.0)
//
// Applied during the final render-target → backbuffer blit.
// On NVIDIA/AMD discrete GPUs the driver silently enables GL_FRAMEBUFFER_SRGB
// and applies a linear→sRGB conversion on every backbuffer write, lifting
// mid-tones and making the image look washed out.  This shader pre-darkens
// the pixels with the inverse of that curve (pow(c, gamma)) so the two
// operations cancel out and the image reaches the monitor at the original
// intended brightness.
//
// gamma = 1.0  → identity (no correction, correct on Intel integrated)
// gamma = 2.2  → full inverse-sRGB pre-darkening (correct on NVIDIA/AMD)
// Values between 1.0 and 2.2 allow per-user tuning via gameconfig.json.

sampler2D ScreenSampler : register(s0);

float Gamma;   // set from GameConfig.Gamma each frame

float4 GammaCorrectPS(float2 uv : TEXCOORD0) : COLOR0
{
    float4 colour = tex2D(ScreenSampler, uv);
    // Apply inverse gamma to RGB; leave alpha untouched.
    float inv = 1.0 / max(Gamma, 0.1);
    colour.rgb = pow(abs(colour.rgb), inv);
    return colour;
}

technique GammaCorrect
{
    pass P0
    {
        PixelShader = compile ps_3_0 GammaCorrectPS();
    }
}
