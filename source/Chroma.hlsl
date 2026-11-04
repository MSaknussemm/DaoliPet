sampler2D Input : register(s0);
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 c = tex2D(Input, uv);
    float limit = max(c.r, c.b);
    float green = (c.g - limit) / max(c.a, 0.00001);
    float keep = 1 - smoothstep(0.02, 0.16, green);
    c.g = min(c.g, limit);
    return c * keep;
}
