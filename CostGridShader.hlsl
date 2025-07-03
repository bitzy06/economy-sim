Texture2D<float> elevationTex : register(t0);
Texture2D<float> waterTex : register(t1);
RWTexture2D<float> costTex : register(u0);

static const int2 Offsets[8] = {
    int2(-1,-1), int2(0,-1), int2(1,-1),
    int2(-1,0),               int2(1,0),
    int2(-1,1),  int2(0,1),  int2(1,1)
};

[numthreads(16,16,1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint2 coord = id.xy;
    float elev = elevationTex[coord];
    float water = waterTex[coord];
    float slope = 0.0f;
    int2 size;
    elevationTex.GetDimensions(size.x, size.y);

    for (int i = 0; i < 8; ++i)
    {
        int2 n = coord + Offsets[i];
        n = clamp(n, int2(0,0), size - 1);
        float neigh = elevationTex[n];
        slope += abs(neigh - elev);
    }
    slope /= 8.0f;
    float cost = slope * 50.0f + water * 1000.0f;
    costTex[coord] = cost;
}
