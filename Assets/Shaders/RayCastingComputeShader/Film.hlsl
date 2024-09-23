// Film.hlsl

#ifndef __FILM__
#define __FILM__

// Uniform Variable
RWTexture2D<float4> frameBuffer;
RWTexture2D<float4> depthBuffer;

// Uniform Variable
uint renderWidth;

// Uniform Variable
uint renderHeight;

uint GetFilmWidth()
{
    return renderWidth;
}

uint GetFilmHeight()
{
    return renderHeight;
}

uint2 GetFilmGeometry()
{
    return uint2(renderWidth, renderHeight);
}

void SetFilmPixel(uint2 index, float4 value)
{
    frameBuffer[index] = value;
}

void SetDepthPixel(uint2 index, float value)
{
    depthBuffer[index] = float4((float3) value, 1.0f);
}

#endif // __FILM__