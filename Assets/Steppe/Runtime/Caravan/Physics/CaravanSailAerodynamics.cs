using UnityEngine;

namespace Steppe.Caravan
{
    public readonly struct CaravanSailForceSample
    {
        public CaravanSailForceSample(
            Vector3 force,
            Vector3 apparentWind,
            float effectiveness,
            float normalizedLoad)
        {
            Force = force;
            ApparentWind = apparentWind;
            Effectiveness = effectiveness;
            NormalizedLoad = normalizedLoad;
        }

        public Vector3 Force { get; }
        public Vector3 ApparentWind { get; }
        public float Effectiveness { get; }
        public float NormalizedLoad { get; }
    }

    /// <summary>
    /// Deliberately compact land-sail model. The sail receives the component of
    /// apparent wind normal to its plane. Wheel side grip later converts part of that
    /// lateral force into useful forward motion without hidden steering assistance.
    /// </summary>
    public static class CaravanSailAerodynamics
    {
        private const float AirDensity = 1.225f;
        private const float LandSailPressureCoefficient = 1.3f;

        public static CaravanSailForceSample Evaluate(
            Vector3 surfaceWind,
            Vector3 vehicleVelocity,
            Vector3 sailNormal,
            float area,
            float moduleEfficiency,
            float maximumForce)
        {
            var apparent = Vector3.ProjectOnPlane(surfaceWind - vehicleVelocity, Vector3.up);
            var speed = apparent.magnitude;
            var normalizedNormal = Vector3.ProjectOnPlane(sailNormal, Vector3.up).normalized;
            if (speed < 0.01f || normalizedNormal.sqrMagnitude < 0.5f || area <= 0f)
            {
                return new CaravanSailForceSample(Vector3.zero, apparent, 0f, 0f);
            }

            var signedNormalSpeed = Vector3.Dot(apparent, normalizedNormal);
            var forceMagnitude = 0.5f
                                 * AirDensity
                                 * LandSailPressureCoefficient
                                 * Mathf.Max(0f, area)
                                 * signedNormalSpeed
                                 * Mathf.Abs(signedNormalSpeed)
                                 * Mathf.Clamp01(moduleEfficiency);
            var unclampedForce = normalizedNormal * forceMagnitude;
            var forceLimit = Mathf.Max(1f, maximumForce);
            var force = Vector3.ClampMagnitude(unclampedForce, forceLimit);
            var effectiveness = Mathf.Clamp01(Mathf.Abs(signedNormalSpeed) / Mathf.Max(0.01f, speed));
            var normalizedLoad = Mathf.Clamp01(unclampedForce.magnitude / forceLimit);
            return new CaravanSailForceSample(force, apparent, effectiveness, normalizedLoad);
        }
    }
}
