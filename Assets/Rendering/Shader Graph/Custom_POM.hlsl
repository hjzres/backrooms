#ifndef TRIPLANAR_POM_INCLUDED
#define TRIPLANAR_POM_INCLUDED

float2 ParallaxOcclusionMapping(float2 uv, float3 viewDir, float heightScale, int steps)
{
    steps = ceil(steps);
    float layerDepth = 1.0 / steps;
    float currentDepth = 0.0;

    float2 deltaUV =
        (viewDir.xy / max(viewDir.z, 0.0001)) * heightScale / steps;

    float2 currentUV = uv;
    float currentHeight = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, currentUV).r;

    for (int i = 0; i < 32; i++)
    {
        if (currentDepth >= currentHeight)
            break;

        currentUV -= deltaUV;
        currentHeight = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, currentUV).r;

        currentDepth += layerDepth;
    }

    float2 prevUV = currentUV + deltaUV;
    float prevHeight = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, prevUV).r;

    float depthAfter = currentHeight - currentDepth;
    float depthBefore = prevHeight - (currentDepth - layerDepth);

    float weight = depthAfter / (depthAfter - depthBefore);
    return lerp(currentUV, prevUV, weight);
}

void TriplanarPOM_float(float3 WorldNormal, float3 ViewDirWS, float3 WorldPos, int Steps, float HeightScale, float Tiling, out float2 UV_X, out float2 UV_Y, out float2 UV_Z)
{
    float3 n = normalize(WorldNormal);
    float3 v = normalize(ViewDirWS);

    // ---------- X axis (YZ plane) ----------
    {
        float s = sign(n.x);

        float2 uv = WorldPos.zy * Tiling;
        uv.x *= s;

        float3 view = float3(v.z, v.y, abs(v.x));
        view.x *= s;

        UV_X = ParallaxOcclusionMapping(uv, view, HeightScale, Steps);
    }

    // ---------- Y axis (XZ plane) ----------
    {
        float s = sign(n.y);

        float2 uv = WorldPos.xz * Tiling;
        uv.x *= s;

        float3 view = float3(v.x, v.z, abs(v.y));
        view.x *= s;

        UV_Y = ParallaxOcclusionMapping(uv, view, HeightScale, Steps);
    }

    // ---------- Z axis (XY plane) ----------
    {
        float s = sign(n.z);

        float2 uv = WorldPos.xy * Tiling;
        uv.x *= s;

        float3 view = float3(v.x, v.y, abs(v.z));
        view.x *= s;

        UV_Z = ParallaxOcclusionMapping(uv, view, HeightScale, Steps);
    }
}

#endif