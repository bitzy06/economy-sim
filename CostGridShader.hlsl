// HLSL equivalent of CostGridShader

Texture2D<float> elevation : register(t0);
Texture2D<float> water : register(t1);
RWTexture2D<float> cost : register(u0);

[numthreads(8,8,1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float eCenter = elevation[id.xy];
    float slope = 0.0;
    [unroll]
    for(int oy = -1; oy <= 1; oy++)
    {
        [unroll]
        for(int ox = -1; ox <= 1; ox++)
        {
            if(ox == 0 && oy == 0) continue;
            int2 n = clamp(int2(id.x + ox, id.y + oy), int2(0,0), int2(elevation.GetDimensions() - 1));
            float e = elevation[n];
            slope += abs(e - eCenter);
        }
    }
    slope /= 8.0;
    float waterCost = water[id.xy];
    cost[id.xy] = slope * 50.0 + waterCost * 1000.0;
}
