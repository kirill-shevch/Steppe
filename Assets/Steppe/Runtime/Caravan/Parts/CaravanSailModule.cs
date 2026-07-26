using System;
using Steppe.Weather;
using UnityEngine;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanSailModule : MonoBehaviour
    {
        private Rigidbody caravanBody;
        private CaravanChassisController chassis;
        private CaravanEnvironmentSampler environment;
        private CaravanModule module;
        private Transform sailPivot;
        private Transform cloth;
        private float area;
        private float maximumForce;
        private float trimDegrees;
        private float lastDustExposure;

        public float TrimDegrees => trimDegrees;
        public float NormalizedTrim => trimDegrees / 82f;
        public CaravanSailForceSample CurrentForce { get; private set; }
        public CaravanModule Module => module;

        public void Configure(
            Rigidbody body,
            CaravanEnvironmentSampler environmentSampler,
            Transform pivot,
            Transform clothTransform,
            float sailArea = 12f,
            float forceLimit = 9500f)
        {
            caravanBody = body != null ? body : throw new ArgumentNullException(nameof(body));
            chassis = caravanBody.GetComponent<CaravanChassisController>();
            environment = environmentSampler ?? throw new ArgumentNullException(nameof(environmentSampler));
            sailPivot = pivot != null ? pivot : throw new ArgumentNullException(nameof(pivot));
            cloth = clothTransform;
            area = Mathf.Max(0.5f, sailArea);
            maximumForce = Mathf.Max(100f, forceLimit);
            module = GetComponent<CaravanModule>();
            SetTrimDegrees(28f);
        }

        public void SetTrimNormalized(float value)
        {
            SetTrimDegrees(Mathf.Clamp(value, -1f, 1f) * 82f);
        }

        public void SetTrimDegrees(float value)
        {
            trimDegrees = Mathf.Clamp(value, -82f, 82f);
            if (sailPivot != null)
            {
                sailPivot.localRotation = Quaternion.Euler(0f, trimDegrees, 0f);
            }
        }

        private void FixedUpdate()
        {
            if (caravanBody == null || sailPivot == null || module == null || !isActiveAndEnabled)
            {
                return;
            }

            if (!environment.TrySample(transform.position, out var sample))
            {
                CurrentForce = default;
                chassis?.ApplySailForce(
                    Vector3.zero,
                    sailPivot != null ? sailPivot.position : caravanBody.position);
                return;
            }

            var wind = new Vector3(sample.Weather.SurfaceWind.x, 0f, sample.Weather.SurfaceWind.y);
            CurrentForce = CaravanSailAerodynamics.Evaluate(
                wind,
                caravanBody.linearVelocity,
                sailPivot.forward,
                area,
                module.State.Efficiency,
                maximumForce);
            module.SetLoad(CurrentForce.NormalizedLoad);

            if (!caravanBody.isKinematic)
            {
                if (chassis != null)
                {
                    chassis.ApplySailForce(CurrentForce.Force, sailPivot.position);
                }
                else
                {
                    caravanBody.AddForceAtPosition(
                        CurrentForce.Force,
                        sailPivot.position,
                        ForceMode.Force);
                }
            }

            lastDustExposure = (float)sample.Dust.Emission;
            module.AccumulateDust(lastDustExposure * UnityEngine.Time.fixedDeltaTime * 0.0025f);
            if (CurrentForce.NormalizedLoad > 0.88f)
            {
                module.Damage(
                    (CurrentForce.NormalizedLoad - 0.88f)
                    * UnityEngine.Time.fixedDeltaTime
                    * 0.0025f);
            }
        }

        private void LateUpdate()
        {
            if (cloth == null)
            {
                return;
            }

            var load = module != null ? module.State.Load : 0f;
            var scale = cloth.localScale;
            scale.z = Mathf.Lerp(0.035f, 0.12f, load);
            cloth.localScale = scale;
            var bend = Mathf.Lerp(-5f, 12f, load) * Mathf.Sign(trimDegrees == 0f ? 1f : trimDegrees);
            cloth.localRotation = Quaternion.Euler(0f, 0f, bend);
        }

        private void OnDisable()
        {
            if (chassis != null)
            {
                chassis.ApplySailForce(
                    Vector3.zero,
                    sailPivot != null ? sailPivot.position : caravanBody.position);
            }
        }
    }

    /// <summary>
    /// A mast-top weather vane driven by the real surface-wind field. Its arrow head
    /// points into the wind while the broad tail follows the moving air mass.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanWindVane : MonoBehaviour
    {
        private const float CalmWindThreshold = 0.15f;

        private CaravanEnvironmentSampler environment;
        private Transform vanePivot;

        public Vector3 CurrentWindVelocity { get; private set; }
        public Vector3 ArrowDirection { get; private set; } = Vector3.forward;
        public float WindSpeed => CurrentWindVelocity.magnitude;

        public void Configure(
            CaravanEnvironmentSampler environmentSampler,
            Transform pivot)
        {
            environment = environmentSampler
                          ?? throw new ArgumentNullException(nameof(environmentSampler));
            vanePivot = pivot != null ? pivot : throw new ArgumentNullException(nameof(pivot));
            UpdateVane(true);
        }

        public static Vector3 GetArrowDirection(Vector2 surfaceWind)
        {
            var velocity = new Vector3(surfaceWind.x, 0f, surfaceWind.y);
            return velocity.sqrMagnitude > CalmWindThreshold * CalmWindThreshold
                ? -velocity.normalized
                : Vector3.zero;
        }

        private void LateUpdate()
        {
            if (environment != null && vanePivot != null)
            {
                UpdateVane(false);
            }
        }

        private void UpdateVane(bool snap)
        {
            var weather = environment.SampleWeather(vanePivot.position);
            CurrentWindVelocity = new Vector3(
                weather.SurfaceWind.x,
                0f,
                weather.SurfaceWind.y);
            var desiredDirection = GetArrowDirection(weather.SurfaceWind);
            if (desiredDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            ArrowDirection = desiredDirection;
            var desiredRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            if (snap)
            {
                vanePivot.rotation = desiredRotation;
                return;
            }

            var response = Mathf.Lerp(2.4f, 9f, Mathf.InverseLerp(0.5f, 14f, WindSpeed));
            var blend = 1f - Mathf.Exp(-response * UnityEngine.Time.deltaTime);
            vanePivot.rotation = Quaternion.Slerp(vanePivot.rotation, desiredRotation, blend);
        }
    }
}
