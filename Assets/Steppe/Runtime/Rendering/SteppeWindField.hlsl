#ifndef STEPPE_WIND_FIELD_INCLUDED
#define STEPPE_WIND_FIELD_INCLUDED

// Published by SteppeWeatherSystem. XY is the canonical XZ velocity, Z is its
// magnitude, and W is a presentation strength normalized around a strong steppe wind.
float4 _SteppeWindVelocity;
float _SteppeWindTime;
// A readable presentation clock: it runs at real speed for x1 and accelerates with
// F6, but deliberately sublinearly so x100 does not turn blade sway into aliasing.
float _SteppeWindAnimationTime;
// X: wavelength along the wind, Y: cross-wind coherence scale,
// Z: fine turbulence scale, W: fraction of physical wind advection used visually.
float4 _SteppeGustScales;

struct SteppeWindFieldSample
{
    float2 direction;
    float broad;
    float fine;
    float strength;
};

float SteppeWindHash21(float2 value)
{
    value = frac(value * float2(123.34, 456.21));
    value += dot(value, value + 45.32);
    return frac(value.x * value.y);
}

float SteppeWindValueNoise(float2 value)
{
    float2 cell = floor(value);
    float2 fraction = frac(value);
    fraction = fraction * fraction * (3.0 - 2.0 * fraction);
    float bottom = lerp(
        SteppeWindHash21(cell),
        SteppeWindHash21(cell + float2(1, 0)),
        fraction.x);
    float top = lerp(
        SteppeWindHash21(cell + float2(0, 1)),
        SteppeWindHash21(cell + float2(1, 1)),
        fraction.x);
    return lerp(bottom, top, fraction.y);
}

SteppeWindFieldSample SampleSteppeWindField(
    float2 canonicalXZ,
    float coherence,
    float plantPhase)
{
    SteppeWindFieldSample result;
    float speed = max(_SteppeWindVelocity.z, 0.001);
    result.direction = _SteppeWindVelocity.xy / speed;

    float broadScale = max(_SteppeGustScales.x, 1.0);
    float crossScale = max(_SteppeGustScales.y, broadScale);
    float fineScale = max(_SteppeGustScales.z, 1.0);
    float travel = _SteppeWindTime * speed * _SteppeGustScales.w;
    float along = dot(canonicalXZ, result.direction) - travel;
    float across = dot(canonicalXZ, float2(-result.direction.y, result.direction.x));

    // A slowly changing cross-wind warp stops the gusts from looking like ruler-straight
    // shader stripes while preserving their very long, readable steppe-scale fronts.
    float warpNoise = SteppeWindValueNoise(float2(
        across / crossScale,
        along / (broadScale * 2.7)));
    float warpedAlong = along + (warpNoise - 0.5) * broadScale * 0.82;
    float wave = 0.5 + 0.5 * sin(warpedAlong / broadScale * 6.2831853);
    float broadNoise = SteppeWindValueNoise(float2(
        warpedAlong / (broadScale * 1.35),
        across / crossScale));
    result.broad = smoothstep(0.12, 0.91, wave * 0.72 + broadNoise * 0.28);

    // Fine motion is also advected, rather than oscillating in object/chunk space. A tiny
    // stable per-plant offset prevents adjacent cards from becoming a rigid marching wall.
    float2 finePosition = canonicalXZ
                          - result.direction * travel * 1.65
                          + float2(plantPhase * 5.7, plantPhase * 2.9);
    result.fine = SteppeWindValueNoise(finePosition / fineScale);

    coherence = saturate(coherence);
    float localResponse = 0.31 + result.fine * 0.61;
    float broadResponse = 0.25 + result.broad * 0.75;
    result.strength = saturate(
        lerp(localResponse, broadResponse, coherence)
        * lerp(0.90, 1.10, result.fine));
    return result;
}

#endif
