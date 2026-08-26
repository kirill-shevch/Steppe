#ifndef STEPPE_CLOUD_SHADOW_INCLUDED
#define STEPPE_CLOUD_SHADOW_INCLUDED

TEXTURE2D(_SteppeCloudWeatherMap);
SAMPLER(sampler_SteppeCloudWeatherMap);

float _SteppeCloudRendererActive;
float4 _SteppeCloudMapCenterLocal;
float4 _SteppeCloudMapAdvectionLocal;
float4 _SteppeCloudLayerParameters;
float _SteppeCloudMapWorldSize;
float3 _SteppeSunDirection;
float _SteppeDaylight;
float _SteppeCloudTransmissionAtFocus;

float SteppeCloudShadowFromWeather(float3 weather)
{
    float opticalDepth = saturate(
        weather.r * 0.32
        + weather.g * 0.92);
    return smoothstep(0.12, 1.0, opticalDepth);
}

float SampleSteppeCloudTransmission(float3 receiverPositionWS)
{
    if (_SteppeCloudRendererActive < 0.5 || _SteppeDaylight <= 0.001)
    {
        return 1.0;
    }

    // Follow the incoming solar ray from the receiver to cloud-base height.
    // This makes long dawn and dusk shadows move away from the cloud column.
    float heightToCloud = max(0.0, _SteppeCloudLayerParameters.x - receiverPositionWS.y);
    float projectionDistance = min(
        heightToCloud / max(_SteppeSunDirection.y, 0.12),
        max(_SteppeCloudLayerParameters.z, 0.0));
    float2 cloudPosition = receiverPositionWS.xz
                           + _SteppeSunDirection.xz * projectionDistance;
    float2 mapPosition = cloudPosition
                         - _SteppeCloudMapCenterLocal.xy
                         - _SteppeCloudMapAdvectionLocal.xy;
    float2 uv = mapPosition / max(_SteppeCloudMapWorldSize, 1.0) + 0.5;
    float edgeDistance = min(uv.x, min(uv.y, min(1.0 - uv.x, 1.0 - uv.y)));
    float mapFade = smoothstep(0.0, 0.055, edgeDistance);
    float3 weather = SAMPLE_TEXTURE2D(
        _SteppeCloudWeatherMap,
        sampler_SteppeCloudWeatherMap,
        saturate(uv)).rgb;
    float cloudShadow = SteppeCloudShadowFromWeather(weather)
                        * mapFade
                        * saturate(_SteppeDaylight);
    return lerp(1.0, 0.06, cloudShadow);
}

float SteppeCloudLightAdjustment(float3 receiverPositionWS)
{
    return SampleSteppeCloudTransmission(receiverPositionWS)
           / max(_SteppeCloudTransmissionAtFocus, 0.06);
}

float SteppeCloudAmbientAdjustment(float3 receiverPositionWS)
{
    float localTransmission = SampleSteppeCloudTransmission(receiverPositionWS);
    float localShadow = saturate((1.0 - localTransmission) / 0.94);
    float focusShadow = saturate((1.0 - _SteppeCloudTransmissionAtFocus) / 0.94);
    float localAmbient = lerp(1.0, 0.58, localShadow);
    float focusAmbient = lerp(1.0, 0.58, focusShadow);
    return localAmbient / max(focusAmbient, 0.58);
}

#endif
