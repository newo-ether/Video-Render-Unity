// Rasterizer.hlsl

#ifndef __RASTERIZER__
#define __RASTERIZER__

struct Triangle
{
    float3 vertex[3];
};

struct Barycentric
{
    bool isInside;
    float3 bcCoord;
};

// Uniform Variable
RWTexture2D<float4> frameBuffer;

// Uniform Variable
RWTexture2D<uint> ZBuffer;

// Uniform Variable
StructuredBuffer<Triangle> triangleBuffer;

// Uniform Variable
int triangleCount;

// Uniform Variable
float4x4 worldToCamera;

// Uniform Variable
float4x4 projectionMatrix;

// Uniform Variable
int textureWidth;

// Uniform Variable
int textureHeight;

uint3 PCG3D(uint3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v ^= v >> 16;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    return v;
}

float3 Vec4ToVec3(float4 v)
{
    return float3(v.xyz / v.w);
}

float cross(float2 v1, float2 v2)
{
    return v1.x * v2.y - v1.y * v2.x;
}

int2 NDCToScreenIndex(float2 p)
{
    float2 screenPos = (p + float2(1.0f, 1.0f)) / 2.0f;
    screenPos.x *= textureWidth;
    screenPos.y *= textureHeight;
    int2 screenIndex = int2(
        min(max((int)screenPos.x, 0), textureWidth - 1),
        min(max((int)screenPos.y, 0), textureHeight - 1)
    );
    return screenIndex;
}

float2 ScreenIndexToNDC(int2 index)
{
    float2 screenPos = float2((index.x + 0.5f) / textureWidth, (index.y + 0.5f) / textureHeight);
    screenPos = screenPos * 2.0f - float2(1.0f, 1.0f);
    return screenPos;
}

Barycentric GetTriangleBarycentric(Triangle tri, float2 p, float3 z)
{
    float2 v0 = float2(tri.vertex[0].x, tri.vertex[0].y);
    float2 v1 = float2(tri.vertex[1].x, tri.vertex[1].y);
    float2 v2 = float2(tri.vertex[2].x, tri.vertex[2].y);

    float2 l0 = v1 - v0;
    float2 l1 = v2 - v1;
    float2 l2 = v0 - v2;

    float b0 = cross(l0, p - v0);
    float b1 = cross(l1, p - v1);
    float b2 = cross(l2, p - v2);

    if ((b0 >= 0.0f && b1 >= 0.0f && b2 >= 0.0f) || (b0 <= 0.0f && b1 <= 0.0f && b2 <= 0.0f))
    {
        Barycentric bc;
        bc.isInside = true;
        bc.bcCoord = float3(b0, b1, b2) / z;
        bc.bcCoord /= bc.bcCoord.x + bc.bcCoord.y + bc.bcCoord.z;
        return bc;
    }
    else
    {
        Barycentric bc;
        bc.isInside = false;
        bc.bcCoord = float3(0.0f, 0.0f, 0.0f);
        return bc;
    }
}

void RasterizeTriangle(int index)
{
    Triangle tri = triangleBuffer[index];
    Triangle triCam, triNDC;
    float2 boundMin = (float2) 1.#INF;
    float2 boundMax = (float2) -1.#INF;

    [unroll]
    for (int n = 0; n < 3; n++)
    {
        triCam.vertex[n] = Vec4ToVec3(mul(worldToCamera, float4(tri.vertex[n], 1.0f)));
        triNDC.vertex[n] = Vec4ToVec3(mul(projectionMatrix, float4(triCam.vertex[n], 1.0f)));
        triNDC.vertex[n].xy *= -1.0f;
        boundMin = min(boundMin, float2(triNDC.vertex[n].x, triNDC.vertex[n].y));
        boundMax = max(boundMax, float2(triNDC.vertex[n].x, triNDC.vertex[n].y));
    }

    int2 begin = NDCToScreenIndex(boundMin);
    int2 end = NDCToScreenIndex(boundMax);

    for (int i = begin.x; i <= end.x; i++)
    {
        for (int j = begin.y; j <= end.y; j++)
        {
            float2 p = ScreenIndexToNDC(int2(i, j));
            Barycentric bc = GetTriangleBarycentric(triNDC, p, float3(triCam.vertex[2].z, triCam.vertex[0].z, triCam.vertex[1].z));
            if (bc.isInside)
            {
                float depth = dot(bc.bcCoord, float3(triNDC.vertex[2].z, triNDC.vertex[0].z, triNDC.vertex[1].z));
                if (depth < 0.0f)
                {
                    continue;
                }
                uint depthInt = asuint(depth);
                uint prevDepthInt;
                InterlockedMax(ZBuffer[uint2(i, j)], depthInt, prevDepthInt);
                if (depthInt > prevDepthInt)
                {
                    float4 color = float4(float3(PCG3D((uint3) index)) / (float3) 4294967295.0f, 1.0f);
                    frameBuffer[uint2(i, j)] = color;
                }
            }
        }
    }
}

void ClearPixel(uint2 index)
{
    frameBuffer[index] = float4(0.0f, 0.0f, 0.0f, 1.0f);
    ZBuffer[index] = 0u;
}

#endif // __RASTERIZER__
