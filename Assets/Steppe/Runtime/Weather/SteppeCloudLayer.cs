using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Weather
{
    /// <summary>
    /// A single camera-centred cloud deck for the whole streamed horizon. P3 intentionally
    /// avoids one object per cloud or per terrain chunk: shape and water come from the
    /// shared weather map, while this mesh contributes one renderer and one material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeCloudLayer : MonoBehaviour
    {
        private static readonly int WeatherMapId = Shader.PropertyToID("_WeatherMap");
        private static readonly int WeatherMapCenterLocalId = Shader.PropertyToID("_WeatherMapCenterLocal");
        private static readonly int WeatherMapWorldSizeId = Shader.PropertyToID("_WeatherMapWorldSize");
        private static readonly int WeatherMapAdvectionLocalId = Shader.PropertyToID("_WeatherMapAdvectionLocal");
        private static readonly int CloudNoiseId = Shader.PropertyToID("_CloudNoise");
        private static readonly int CloudNoiseWorldPhaseId = Shader.PropertyToID("_CloudNoiseWorldPhase");
        private static readonly int CloudNoiseAdvectionLocalId = Shader.PropertyToID("_CloudNoiseAdvectionLocal");
        private static readonly int CloudNoiseWorldSizeId = Shader.PropertyToID("_CloudNoiseWorldSize");
        private const float CloudNoiseWorldSize = 16000f;

        private SteppeWorldSettings settings;
        private SteppeWeatherSystem weatherSystem;
        private FloatingOriginSystem floatingOrigin;
        private Mesh cloudMesh;
        private Material cloudMaterial;
        private Texture2D cloudNoise;
        private bool ownsMaterial;

        public MeshRenderer Renderer { get; private set; }

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeWeatherSystem weather,
            FloatingOriginSystem origin,
            Material material = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));

            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            Renderer = gameObject.GetComponent<MeshRenderer>();
            if (Renderer == null)
            {
                Renderer = gameObject.AddComponent<MeshRenderer>();
            }
            cloudMesh = BuildDisc(settings.CloudLayerRadius, 16, 96);
            meshFilter.sharedMesh = cloudMesh;

            if (material != null)
            {
                cloudMaterial = material;
                ownsMaterial = false;
            }
            else
            {
                var shader = Shader.Find("Steppe/Cloud Layer");
                if (shader == null)
                {
                    throw new InvalidOperationException("Steppe/Cloud Layer shader was not found.");
                }

                cloudMaterial = new Material(shader)
                {
                    name = "Steppe P3 Cloud Layer Material",
                    hideFlags = HideFlags.DontSave
                };
                ownsMaterial = true;
            }

            Renderer.sharedMaterial = cloudMaterial;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.lightProbeUsage = LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            cloudNoise = BuildCloudNoise(128, settings.WorldSeed + 40933);
            cloudMaterial.SetTexture(CloudNoiseId, cloudNoise);
            cloudMaterial.SetFloat(CloudNoiseWorldSizeId, CloudNoiseWorldSize);
            ApplyWeatherMap();
        }

        private void LateUpdate()
        {
            if (settings == null || weatherSystem == null || floatingOrigin == null || cloudMaterial == null)
            {
                return;
            }

            ApplyWeatherMap();
        }

        private void ApplyWeatherMap()
        {
            var center = floatingOrigin.WorldToLocal(
                weatherSystem.MapCenterX,
                settings.CloudBaseHeight,
                weatherSystem.MapCenterZ);
            transform.position = center;
            cloudMaterial.SetTexture(WeatherMapId, weatherSystem.WeatherMap);
            cloudMaterial.SetVector(WeatherMapCenterLocalId, new Vector4(center.x, center.z, 0f, 0f));
            cloudMaterial.SetFloat(WeatherMapWorldSizeId, weatherSystem.MapWorldSize);
            var elapsedSinceMap = weatherSystem.WeatherSeconds - weatherSystem.PublishedMapWeatherSeconds;
            var advection = weatherSystem.Model.WindVelocity * (float)elapsedSinceMap;
            cloudMaterial.SetVector(WeatherMapAdvectionLocalId, new Vector4(advection.x, advection.y, 0f, 0f));
            cloudMaterial.SetVector(CloudNoiseWorldPhaseId, new Vector4(
                PositiveModulo(weatherSystem.MapCenterX, CloudNoiseWorldSize),
                PositiveModulo(weatherSystem.MapCenterZ, CloudNoiseWorldSize),
                0f,
                0f));
            var wind = weatherSystem.Model.WindVelocity;
            cloudMaterial.SetVector(CloudNoiseAdvectionLocalId, new Vector4(
                PositiveModulo(wind.x * weatherSystem.WeatherSeconds, CloudNoiseWorldSize),
                PositiveModulo(wind.y * weatherSystem.WeatherSeconds, CloudNoiseWorldSize),
                0f,
                0f));
        }

        private static Texture2D BuildCloudNoise(int size, int seed)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "Steppe Tileable Cloud Detail",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 1
            };
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var low = PeriodicValueNoise(x, y, size, 5, seed);
                    var middle = PeriodicValueNoise(x, y, size, 13, seed + 1013);
                    var fine = PeriodicValueNoise(x, y, size, 31, seed + 2027);
                    var cellular = PeriodicCellularNoise(x, y, size, 9, seed + 3041);
                    var structure = Mathf.Clamp01(
                        low * 0.38f
                        + middle * 0.30f
                        + fine * 0.10f
                        + cellular * 0.22f);
                    pixels[y * size + x] = new Color32(
                        ToByte(structure),
                        ToByte(middle),
                        ToByte(fine),
                        ToByte(cellular));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float PeriodicValueNoise(int pixelX, int pixelY, int textureSize, int period, int seed)
        {
            var sampleX = pixelX / (float)textureSize * period;
            var sampleY = pixelY / (float)textureSize * period;
            var x0 = Mathf.FloorToInt(sampleX);
            var y0 = Mathf.FloorToInt(sampleY);
            var x1 = (x0 + 1) % period;
            var y1 = (y0 + 1) % period;
            x0 %= period;
            y0 %= period;
            var tx = Smooth(sampleX - Mathf.Floor(sampleX));
            var ty = Smooth(sampleY - Mathf.Floor(sampleY));
            var bottom = Mathf.Lerp(Hash01(x0, y0, seed), Hash01(x1, y0, seed), tx);
            var top = Mathf.Lerp(Hash01(x0, y1, seed), Hash01(x1, y1, seed), tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        private static float Hash01(int x, int y, int seed)
        {
            var hash = DeterministicNoise.Hash(x, y, seed);
            return (hash & 0x00ffffffUL) / 16777215f;
        }

        private static float PeriodicCellularNoise(
            int pixelX,
            int pixelY,
            int textureSize,
            int period,
            int seed)
        {
            var sampleX = pixelX / (float)textureSize * period;
            var sampleY = pixelY / (float)textureSize * period;
            var cellX = Mathf.FloorToInt(sampleX);
            var cellY = Mathf.FloorToInt(sampleY);
            var nearestSquared = 4f;

            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    var candidateX = cellX + offsetX;
                    var candidateY = cellY + offsetY;
                    var wrappedX = PositiveModulo(candidateX, period);
                    var wrappedY = PositiveModulo(candidateY, period);
                    var featureX = candidateX + Hash01(wrappedX, wrappedY, seed);
                    var featureY = candidateY + Hash01(wrappedX, wrappedY, seed + 7919);
                    var deltaX = sampleX - featureX;
                    var deltaY = sampleY - featureY;
                    nearestSquared = Mathf.Min(nearestSquared, deltaX * deltaX + deltaY * deltaY);
                }
            }

            return 1f - Mathf.Clamp01(Mathf.Sqrt(nearestSquared) / 1.05f);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            return (value % modulus + modulus) % modulus;
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static float PositiveModulo(double value, double modulus)
        {
            return (float)(value - Math.Floor(value / modulus) * modulus);
        }

        private static Mesh BuildDisc(float radius, int ringCount, int segmentCount)
        {
            var vertices = new List<Vector3>(1 + ringCount * segmentCount) { Vector3.zero };
            var colors = new List<Color32>(vertices.Capacity) { new Color32(255, 255, 255, 255) };
            var triangles = new List<int>(segmentCount * 3 + (ringCount - 1) * segmentCount * 6);

            for (var ring = 1; ring <= ringCount; ring++)
            {
                var ringFraction = ring / (float)ringCount;
                var ringRadius = radius * ringFraction;
                var height = ringFraction * ringFraction * 140f;
                var edgeFade = 1f - Mathf.SmoothStep(0.78f, 1f, ringFraction);
                var alpha = (byte)Mathf.RoundToInt(edgeFade * 255f);

                for (var segment = 0; segment < segmentCount; segment++)
                {
                    var angle = segment / (float)segmentCount * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        Mathf.Sin(angle) * ringRadius,
                        height,
                        Mathf.Cos(angle) * ringRadius));
                    colors.Add(new Color32(255, 255, 255, alpha));
                }
            }

            for (var segment = 0; segment < segmentCount; segment++)
            {
                var next = (segment + 1) % segmentCount;
                triangles.Add(0);
                triangles.Add(1 + segment);
                triangles.Add(1 + next);
            }

            for (var ring = 1; ring < ringCount; ring++)
            {
                var innerStart = 1 + (ring - 1) * segmentCount;
                var outerStart = 1 + ring * segmentCount;
                for (var segment = 0; segment < segmentCount; segment++)
                {
                    var next = (segment + 1) % segmentCount;
                    var inner = innerStart + segment;
                    var innerNext = innerStart + next;
                    var outer = outerStart + segment;
                    var outerNext = outerStart + next;

                    triangles.Add(inner);
                    triangles.Add(outer);
                    triangles.Add(outerNext);
                    triangles.Add(inner);
                    triangles.Add(outerNext);
                    triangles.Add(innerNext);
                }
            }

            var mesh = new Mesh
            {
                name = "Steppe P3 Cloud Deck",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(Vector3.up * 70f, new Vector3(radius * 2f, 400f, radius * 2f));
            return mesh;
        }

        private void OnDestroy()
        {
            DestroyOwned(cloudMesh);
            DestroyOwned(cloudNoise);
            if (ownsMaterial)
            {
                DestroyOwned(cloudMaterial);
            }
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
