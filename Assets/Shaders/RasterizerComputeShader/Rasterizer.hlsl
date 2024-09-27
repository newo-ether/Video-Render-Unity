// Rasterizer.hlsl

#ifndef __RASTERIZER__
#define __RASTERIZER__

#define INF 1.#INF

struct Triangle
{
    float4 vertex[3];
};

struct Barycentric
{
    bool isInside;
    float3 bcCoord;
};

// Uniform Variable
RWTexture2D<float4> frameBuffer;

// Uniform Variable
RWTexture2D<float4> ZBuffer;

// Uniform Variable
StructuredBuffer<Triangle> triangleBuffer;

// Uniform Variable
RWStructuredBuffer<Triangle> triangleNDCBuffer;

// Uniform Variable
RWStructuredBuffer<Triangle> triangleNDCDivWBuffer;

// Uniform Variable
RWStructuredBuffer<uint4> triangleBoundBuffer;

// Uniform Variable
uint triangleCount;

// Uniform Variable
uint clippedTriangleCount;

// Uniform Variable
float4x4 worldToNDC;

// Uniform Variable
int textureWidth;

// Uniform Variable
int textureHeight;

// Uniform Variable
uint triangleClampMin;

// Uniform Variable
uint triangleClampMax;

// Uniform Variable
float triangleAppear;

// Uniform Variable
bool disableZBuffer;

// Uniform Variable
uint triangleOne;

// Uniform Variable
uint triangleTwo;

// Uniform Variable
float triangleOneAppear;

// Uniform Variable
float triangleTwoAppear;

float3 Rand(int index)
{
    float floatIndex = (float) index;
    return float3(
        frac(sqrt(234.928f * floatIndex + 12.532f)),
        frac(sqrt(533.425f * floatIndex + 43.913f)),
        frac(sqrt(732.563f * floatIndex + 69.729f)));
}

float4 VectorDivW(float4 v)
{
    return float4(v.xyz / v.w, 1.0f);
}

Triangle TriangleDivW(Triangle tri)
{
    Triangle triNew;

    [unroll]
    for (int i = 0; i < 3; i++)
    {
        triNew.vertex[i] = VectorDivW(tri.vertex[i]);
    }

    return triNew;
}

Triangle TriangleInitEmpty()
{
    Triangle tri;

    tri.vertex[0] = float4((float3) 0.0f, 1.0f);
    tri.vertex[1] = float4((float3) 0.0f, 1.0f);
    tri.vertex[2] = float4((float3) 0.0f, 1.0f);

    return tri;
}

bool IsEmptyTriangle(int index)
{
    Triangle tri = triangleNDCBuffer[index];
    
    return tri.vertex[0].x == 0.0f && tri.vertex[0].y == 0.0f && tri.vertex[0].z == 0.0f &&
           tri.vertex[1].x == 0.0f && tri.vertex[1].y == 0.0f && tri.vertex[1].z == 0.0f &&
           tri.vertex[2].x == 0.0f && tri.vertex[2].y == 0.0f && tri.vertex[2].z == 0.0f;
}

void ClearTriangleNDCBuffer(int index)
{
    triangleNDCBuffer[index] = TriangleInitEmpty();
}

float cross(float2 v1, float2 v2)
{
    return v1.x * v2.y - v1.y * v2.x;
}

uint GetTriangleCount()
{
    return triangleCount;
}

uint GetClippedTriangleCount()
{
    return clippedTriangleCount;
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

void WorldToNDCTransform(int index)
{
    [unroll]
    for (int n = 0; n < 3; n++)
    {
        float4 vertexNDC = mul(worldToNDC, triangleBuffer[index].vertex[n]);
        triangleNDCBuffer[index].vertex[n] = vertexNDC;
    }
}

void Clipping(int index)
{
    Triangle tri = triangleNDCBuffer[index];
    bool3 isInside = bool3(tri.vertex[0].z >= 0.0f, tri.vertex[1].z >= 0.0f, tri.vertex[2].z >= 0.0f);
    
    int n;
    int insideCount = 0;

    [unroll]
    for (n = 0; n < 3; n++)
    {
        if (isInside[n])
        {
            insideCount++;
        }
    }

    if (insideCount == 0)
    {
        triangleNDCBuffer[index] = TriangleInitEmpty();
        return;
    }
    
    if (insideCount == 3)
    {
        return;
    }

    float4 firstPoint = (float4) 0.0f;
    float4 secondPoint = (float4) 0.0f;
    int firstSegmentIndex[2] = { 0, 0 };
    int secondSegmentIndex[2] = { 0, 0 };
    bool isFirstPointSet = false;
    bool isSecondPointSet = false;

    [unroll]
    for (n = 0; n < 3; n++)
    {
        if (isInside[n] ^ isInside[(n + 1) % 3])
        {
            float4 p0 = tri.vertex[n];
            float4 p1 = tri.vertex[(n + 1) % 3];
            float t = (0.0f - p0.z) / (p1.z - p0.z);

            if (isFirstPointSet)
            {
                secondPoint = p0 + t * (p1 - p0);
                secondSegmentIndex[0] = n;
                secondSegmentIndex[1] = (n + 1) % 3;
                isSecondPointSet = true;
            }
            else
            {
                firstPoint = p0 + t * (p1 - p0);
                firstSegmentIndex[0] = n;
                firstSegmentIndex[1] = (n + 1) % 3;
                isFirstPointSet = true;
            }
        }
    }

    if (isFirstPointSet && isSecondPointSet)
    {
        if (insideCount == 1)
        {
            Triangle newTri;
            float4 originalPoint = tri.vertex[isInside.x ? 0 : (isInside.y ? 1 : 2)];
            
            newTri.vertex[0] = firstPoint;
            newTri.vertex[1] = secondPoint;
            newTri.vertex[2] = originalPoint;

            triangleNDCBuffer[index] = newTri;
        }
        else if (insideCount == 2)
        {
            float4 firstOriginalPoint = (float4) 0.0f;
            float4 secondOriginalPoint = (float4) 0.0f;
            int firstOriginalPointIndex = 0;
            int secondOriginalPointIndex = 0;
            bool isFirstOriginalPointSet = false;
            bool isSecondOriginalPointSet = false;

            [unroll]
            for (n = 0; n < 3; n++)
            {
                if (isInside[n])
                {
                    if (isFirstOriginalPointSet)
                    {
                        secondOriginalPoint = tri.vertex[n];
                        secondOriginalPointIndex = n;
                        isSecondOriginalPointSet = true;
                    }
                    else
                    {
                        firstOriginalPoint = tri.vertex[n];
                        firstOriginalPointIndex = n;
                        isFirstOriginalPointSet = true;
                    }
                }
            }

            if (isFirstOriginalPointSet && isSecondOriginalPointSet)
            {
                if (firstSegmentIndex[0] == secondOriginalPointIndex || firstSegmentIndex[1] == secondOriginalPointIndex)
                {
                    float4 temp = secondPoint;
                    secondPoint = firstPoint;
                    firstPoint = temp;
                }
                
                Triangle newTris[2];
                
                newTris[0].vertex[0] = firstOriginalPoint;
                newTris[0].vertex[1] = secondOriginalPoint;
                newTris[0].vertex[2] = firstPoint;
                
                newTris[1].vertex[0] = firstPoint;
                newTris[1].vertex[1] = secondPoint;
                newTris[1].vertex[2] = secondOriginalPoint;
                
                triangleNDCBuffer[index] = newTris[0];
                triangleNDCBuffer[index + triangleCount] = newTris[1];
            }
        }
    }
}

void PerspectiveDivision(int index)
{
    triangleNDCDivWBuffer[index] = TriangleDivW(triangleNDCBuffer[index]);
}

void CalculateTriangleBound(int index)
{
    float2 boundMin = (float2) INF;
    float2 boundMax = (float2) -INF;

    Triangle triNDCDivW = triangleNDCDivWBuffer[index];

    [unroll]
    for (int n = 0; n < 3; n++)
    {
        float2 p = triNDCDivW.vertex[n].xy;
        boundMin = min(p, boundMin);
        boundMax = max(p, boundMax);
    }

    triangleBoundBuffer[index] = uint4(
        (uint2) min(max(NDCToScreenIndex(boundMin), (int2) 0), int2(textureWidth - 1, textureHeight - 1)),
        (uint2) min(max(NDCToScreenIndex(boundMax), (int2) 0), int2(textureWidth - 1, textureHeight - 1))
    );
}

Barycentric GetTriangleBarycentric(Triangle triNDC, Triangle triNDCDivW, float2 p)
{
    float2 v0 = triNDCDivW.vertex[0].xy;
    float2 v1 = triNDCDivW.vertex[1].xy;
    float2 v2 = triNDCDivW.vertex[2].xy;

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
        bc.bcCoord = float3(b0, b1, b2) / float3(triNDC.vertex[2].w, triNDC.vertex[0].w, triNDC.vertex[1].w);
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

void RasterizeTriangle(uint3 id)
{
    uint4 bound = triangleBoundBuffer[id.z];

    if ((id.x >= bound.x && id.y >= bound.y) && (id.x <= bound.z && id.y <= bound.w))
    {
        float appear = (float) ((bound.z - bound.x + 1) * (bound.w - id.y) + (id.x - bound.x + 1)) / ((bound.z - bound.x + 1) * (bound.w - bound.y + 1) + 1);
        if ((id.z == triangleOne && appear < triangleOneAppear) ||
            (id.z == triangleTwo && appear < triangleTwoAppear) ||
            (id.z > triangleClampMin && id.z <= triangleClampMax && appear < triangleAppear))
        {
            Triangle triNDC = triangleNDCBuffer[id.z];
            Triangle triNDCDivW = triangleNDCDivWBuffer[id.z];
            float2 p = ScreenIndexToNDC(id.xy);
            Barycentric bc = GetTriangleBarycentric(triNDC, triNDCDivW, p);

            if (bc.isInside)
            {
                float depth = dot(bc.bcCoord, float3(triNDC.vertex[2].z, triNDC.vertex[0].z, triNDC.vertex[1].z));
                float w = dot(bc.bcCoord, float3(triNDC.vertex[2].w, triNDC.vertex[0].w, triNDC.vertex[1].w));
                depth /= w;

                if (!disableZBuffer)
                {
                    if (depth < ZBuffer[id.xy].r)
                    {
                        float4 color = float4(Rand(id.z > triangleCount ? id.z - triangleCount : id.z), 1.0f);
                        frameBuffer[id.xy] = color;
                        ZBuffer[id.xy] = float4((float3) depth, 1.0f);
                    }
                }
                else
                {
                    float4 color = float4(Rand(id.z > triangleCount ? id.z - triangleCount : id.z), 1.0f);
                    frameBuffer[id.xy] = color;
                }
            }
        }
    }
}

void ClearPixel(uint2 index)
{
    frameBuffer[index] = float4(0.0f, 0.0f, 0.0f, 1.0f);
    ZBuffer[index] = float4(1.0f, 1.0f, 1.0f, 1.0f);
}

#endif // __RASTERIZER__
