// IntersectInfo.hlsl

#ifndef __INTERSECTINFO__
#define __INTERSECTINFO__

struct IntersectInfo
{
    bool isHit;
    float tHit;
    float3 hitPoint;
    float3 hitNormal;
    float3 incomeDir;
    int triangleIndex;
};

IntersectInfo IntersectInfoInit(float tHit,
                                float3 hitPoint,
                                float3 hitNormal,
                                float3 incomeDir,
                                int triangleIndex)
{
    IntersectInfo isect =
    {
        true,
        tHit,
        hitPoint,
        hitNormal,
        incomeDir,
        triangleIndex
    };
    return isect;
}

IntersectInfo IntersectInfoInitNone()
{
    IntersectInfo isect =
    {
        false,
        1.#INF,
        (float3) 0.0f,
        (float3) 0.0f,
        (float3) 0.0f,
        0
    };
    return isect;
}

#endif // __INTERSECTINFO__