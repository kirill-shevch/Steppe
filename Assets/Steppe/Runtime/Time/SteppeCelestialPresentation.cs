using System;
using UnityEngine;

namespace Steppe.Time
{
    public sealed class SteppeCelestialPresentation : MonoBehaviour
    {
        private static readonly Color NightFog = new Color(0.055f, 0.075f, 0.11f);
        private static readonly Color DawnFog = new Color(0.62f, 0.44f, 0.34f);
        private static readonly Color DayFog = new Color(0.72f, 0.79f, 0.82f);
        private static readonly Color DawnLight = new Color(1f, 0.58f, 0.35f);
        private static readonly Color DayLight = new Color(1f, 0.94f, 0.83f);
        private static readonly Color MoonLightColor = new Color(0.55f, 0.67f, 0.92f);
        private static readonly Vector3 FixedMoonDirection = new Vector3(-0.34f, 0.57f, 0.75f).normalized;
        private static readonly int NightAmountId = Shader.PropertyToID("_SteppeNightAmount");
        private static readonly int MoonDirectionId = Shader.PropertyToID("_SteppeMoonDirection");
        private static readonly int MoonVisibilityId = Shader.PropertyToID("_SteppeMoonVisibility");

        private SteppeTimeSystem timeSystem;
        private Light sun;
        private Light moon;
        private float latitudeDegrees;
        private Material skyboxMaterial;
        private Material previousSkyboxMaterial;

        public SolarState CurrentSolarState { get; private set; }
        public float MoonVisibility { get; private set; }
        public Light MoonLight => moon;

        public void Configure(
            SteppeTimeSystem clock,
            Light sunLight,
            Light moonLight,
            float latitude)
        {
            timeSystem = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            sun = sunLight != null ? sunLight : throw new ArgumentNullException(nameof(sunLight));
            moon = moonLight != null ? moonLight : throw new ArgumentNullException(nameof(moonLight));
            latitudeDegrees = latitude;
            InstallSkybox();
            ApplyPresentation();
        }

        private void LateUpdate()
        {
            if (timeSystem != null && sun != null && moon != null)
            {
                ApplyPresentation();
            }
        }

        private void ApplyPresentation()
        {
            CurrentSolarState = SteppeAstronomy.Evaluate(timeSystem.Current, latitudeDegrees);
            sun.transform.rotation = Quaternion.LookRotation(-CurrentSolarState.Direction, Vector3.up);

            var daylight = (float)CurrentSolarState.Daylight;
            var elevationLight = Mathf.Clamp01(((float)CurrentSolarState.ElevationDegrees + 2f) / 42f);
            var warmth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)CurrentSolarState.ElevationDegrees / 16f));
            sun.intensity = daylight * Mathf.Lerp(0.12f, 1.15f, elevationLight);
            sun.color = Color.Lerp(DawnLight, DayLight, warmth);

            moon.transform.rotation = Quaternion.LookRotation(-FixedMoonDirection, Vector3.up);
            var starNightAmount = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.02f, 0.58f, daylight));
            var moonAltitude = Mathf.Clamp01((FixedMoonDirection.y + 0.06f) / 0.76f);
            MoonVisibility = starNightAmount;
            moon.intensity = MoonVisibility * Mathf.Lerp(0.018f, 0.075f, moonAltitude);
            moon.color = MoonLightColor;
            moon.enabled = moon.intensity > 0.0001f;
            RenderSettings.sun = moon.intensity > sun.intensity ? moon : sun;

            var dawnAmount = daylight * (1f - warmth);
            var fogColor = Color.Lerp(NightFog, DayFog, daylight);
            fogColor = Color.Lerp(fogColor, DawnFog, dawnAmount * 0.6f);
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = Mathf.Lerp(0.0003f, 0.00018f, daylight);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.16f, 1f, daylight);

            Shader.SetGlobalFloat("_SteppeAbsoluteDay", (float)timeSystem.Current.AbsoluteDay);
            Shader.SetGlobalFloat("_SteppeYearFraction", (float)timeSystem.Current.YearFraction);
            Shader.SetGlobalFloat("_SteppeDaylight", daylight);
            Shader.SetGlobalVector("_SteppeSunDirection", CurrentSolarState.Direction);
            Shader.SetGlobalFloat(NightAmountId, starNightAmount);
            Shader.SetGlobalVector(MoonDirectionId, FixedMoonDirection);
            Shader.SetGlobalFloat(MoonVisibilityId, MoonVisibility);
        }

        private void InstallSkybox()
        {
            var shader = Shader.Find("Steppe/Skybox");
            if (shader == null)
            {
                return;
            }

            previousSkyboxMaterial = RenderSettings.skybox;
            skyboxMaterial = new Material(shader)
            {
                name = "Steppe Procedural Day-Night Skybox",
                hideFlags = HideFlags.DontSave
            };
            RenderSettings.skybox = skyboxMaterial;
        }

        private void OnDestroy()
        {
            if (RenderSettings.skybox == skyboxMaterial)
            {
                RenderSettings.skybox = previousSkyboxMaterial;
            }

            if (RenderSettings.sun == moon && sun != null)
            {
                RenderSettings.sun = sun;
            }

            if (skyboxMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(skyboxMaterial);
            }
            else
            {
                DestroyImmediate(skyboxMaterial);
            }
        }
    }
}
