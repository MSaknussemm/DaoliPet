sampler2D Input : register(s0);
float Time : register(c0);
float AnimateStill : register(c1);
float Sway : register(c2);
float Stretch : register(c3);
float Twist : register(c4);
float Drift : register(c5);
float Wriggle : register(c6);
float Side : register(c7);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 p = uv;
    {
        // Deform the supplied pixels only: no replacement face or drawn mouth.
        // Inverse mapping leaves room for the larger movement inside the window.
        float2 pivot = float2(0.5, 0.80);
        p.x -= Drift * 0.05 * AnimateStill;
        float2 scale = float2(0.84 + 0.23 * Sway, 0.84 + 0.38 * Stretch);
        scale = lerp(float2(1.0, 1.0), scale, AnimateStill);
        p = (p - pivot) / scale + pivot;
        p.x += Twist * (p.y - 0.78) * 0.32 * AnimateStill;
        float sway = Twist * 0.025 * AnimateStill;
        p.x += sway * uv.y * (1.0 - uv.y) * 4.0;
        float y = p.y - 0.55;
        p.x += Wriggle * AnimateStill * 0.27 * y * (1 - 3.5*y*y);
        float side = Side * AnimateStill;
        p.x = 0.5 + (p.x - 0.5) / (1 - 0.40 * abs(side));
        p.y = 0.55 + (p.y - 0.55) * (1 + side * (p.x - 0.5) * 0.32);
        p.x -= side * 0.04;
        float2 mouth = float2(0.52, 0.57);
        float2 d = p - mouth;
        float weight = exp(-dot(d * float2(4.5, 5.5), d * float2(4.5, 5.5)));
        float opening = 0.30 * Time * AnimateStill;
        p.y -= d.y * weight * opening;
        p.x -= d.x * weight * (0.10 * Time * AnimateStill);
    }
    return tex2D(Input, p);
}
