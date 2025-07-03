// Vertex Shader para triângulo texturizado
struct VS_INPUT {
    float3 pos : POSITION;
    float2 uv : TEXCOORD;
};

struct PS_INPUT {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD;
};

PS_INPUT main(VS_INPUT input) {
    PS_INPUT output;
    output.pos = float4(input.pos, 1.0);
    output.uv = input.uv;
    return output;
}
