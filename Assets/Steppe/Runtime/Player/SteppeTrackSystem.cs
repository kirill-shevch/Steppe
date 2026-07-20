using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Time;
using Steppe.World;
using UnityEngine;

namespace Steppe.Player
{
    public readonly struct SteppeTrackSample
    {
        public SteppeTrackSample(double vegetationFlattening, double snowCompression, double soilRut, double wetPrint)
        {
            VegetationFlattening = vegetationFlattening;
            SnowCompression = snowCompression;
            SoilRut = soilRut;
            WetPrint = wetPrint;
        }

        public double VegetationFlattening { get; }
        public double SnowCompression { get; }
        public double SoilRut { get; }
        public double WetPrint { get; }
    }

    public readonly struct SteppeTrackCellCoordinate : IEquatable<SteppeTrackCellCoordinate>
    {
        public SteppeTrackCellCoordinate(long x, long z)
        {
            X = x;
            Z = z;
        }

        public long X { get; }
        public long Z { get; }

        public static SteppeTrackCellCoordinate FromWorld(double worldX, double worldZ, double cellSize)
        {
            return new SteppeTrackCellCoordinate(
                (long)Math.Floor(worldX / cellSize),
                (long)Math.Floor(worldZ / cellSize));
        }

        public double CenterX(double cellSize) => (X + 0.5) * cellSize;
        public double CenterZ(double cellSize) => (Z + 0.5) * cellSize;
        public bool Equals(SteppeTrackCellCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is SteppeTrackCellCoordinate other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Z.GetHashCode());
    }

    /// <summary>
    /// Sparse canonical memory of the rolling ball's passage. The GPU texture is
    /// recentered around the player, while the dictionary survives chunk unloads.
    /// </summary>
    [DefaultExecutionOrder(160)]
    [DisallowMultipleComponent]
    public sealed class SteppeTrackSystem : MonoBehaviour
    {
        private const double ShaderCoordinatePeriod = 65536.0;
        private const double FlattenRecoverySeconds = 5.0 * 86400.0;
        private const double SnowTrackRecoverySeconds = 3.0 * 86400.0;
        private const double RutRecoverySeconds = 30.0 * 86400.0;
        private const double WetPrintRecoverySeconds = 6.0 * 3600.0;
        private static readonly int StateMapId = Shader.PropertyToID("_SteppeTrackStateMap");
        private static readonly int MapOriginSizeId = Shader.PropertyToID("_SteppeTrackMapOriginSize");
        private static readonly int MapParametersId = Shader.PropertyToID("_SteppeTrackMapParameters");
        private static readonly int RutDepthId = Shader.PropertyToID("_SteppeTrackRutDepth");

        private sealed class TrackRecord
        {
            public SteppeTrackSample Sample;
            public double LastSimulationSeconds;
        }

        private readonly Dictionary<SteppeTrackCellCoordinate, TrackRecord> records =
            new Dictionary<SteppeTrackCellCoordinate, TrackRecord>();

        private SteppeWorldSettings settings;
        private SteppeTimeSystem timeSystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private SteppeBallController ball;
        private Texture2D stateMap;
        private Color32[] pixels;
        private SteppeTrackCellCoordinate mapOrigin;
        private SteppeTrackCellCoordinate center;
        private bool hasMapOrigin;
        private bool dirty;
        private float uploadCountdown;
        private float decayRefreshCountdown = 2f;

        public Texture2D StateMap => stateMap;
        public int StoredTrackCellCount => records.Count;
        public int MapRevision { get; private set; }
        public bool IsMapReady => MapRevision > 0;
        public float MapWorldSize => settings != null ? settings.TrackMapWorldSize : 0f;
        public SteppeTrackSample CurrentAtFocus { get; private set; }

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeTimeSystem clock,
            FloatingOriginSystem origin,
            Transform focusTransform,
            SteppeBallController ballController)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            timeSystem = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            ball = ballController != null ? ballController : throw new ArgumentNullException(nameof(ballController));

            stateMap = new Texture2D(
                settings.TrackMapResolution,
                settings.TrackMapResolution,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Steppe Physical Track Map (plants, snow, rut, wet print)",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            pixels = new Color32[settings.TrackMapResolution * settings.TrackMapResolution];
            var world = floatingOrigin.LocalToWorld(focus.position);
            center = SteppeTrackCellCoordinate.FromWorld(world.X, world.Z, settings.TrackCellSize);
            RecenterMap();
        }

        public bool TrySample(double worldX, double worldZ, out SteppeTrackSample sample)
        {
            if (settings == null)
            {
                sample = default;
                return false;
            }

            var coordinate = SteppeTrackCellCoordinate.FromWorld(worldX, worldZ, settings.TrackCellSize);
            if (!records.TryGetValue(coordinate, out var record))
            {
                sample = default;
                return false;
            }

            sample = Decay(record.Sample, timeSystem.ElapsedSimulationSeconds - record.LastSimulationSeconds);
            return true;
        }

        private void FixedUpdate()
        {
            if (settings == null || ball == null || !ball.IsGrounded || ball.Speed < 0.15f)
            {
                return;
            }

            var world = floatingOrigin.LocalToWorld(focus.position);
            var traversal = ball.CurrentSurface;
            var pressure = Mathf.Clamp01(0.32f + ball.Speed / settings.PlayerMaximumSpeed * 0.68f);
            var flattening = pressure * 0.92f;
            var snowCompression = pressure * (float)traversal.SnowSink;
            var soilRut = pressure * Mathf.Clamp01((float)traversal.Mud * 0.9f + (float)traversal.LooseGround * 0.24f);
            var wetPrint = pressure * Mathf.Clamp01((float)traversal.Mud * 0.8f + soilRut * 0.35f);
            Stamp(
                world.X,
                world.Z,
                settings.PlayerBallRadius * 1.12f,
                new SteppeTrackSample(flattening, snowCompression, soilRut, wetPrint));
        }

        private void Update()
        {
            if (settings == null || focus == null)
            {
                return;
            }

            var world = floatingOrigin.LocalToWorld(focus.position);
            var nextCenter = SteppeTrackCellCoordinate.FromWorld(world.X, world.Z, settings.TrackCellSize);
            if (!nextCenter.Equals(center))
            {
                center = nextCenter;
                EnsureMapCoverage();
            }

            TrySample(world.X, world.Z, out var current);
            CurrentAtFocus = current;
            uploadCountdown -= UnityEngine.Time.unscaledDeltaTime;
            decayRefreshCountdown -= UnityEngine.Time.unscaledDeltaTime;
            if (records.Count > 0 && decayRefreshCountdown <= 0f)
            {
                RefreshVisibleMapAndPrune();
                decayRefreshCountdown = 2f;
            }
            if (dirty && uploadCountdown <= 0f)
            {
                UploadMap();
            }
        }

        private void Stamp(double worldX, double worldZ, double radius, SteppeTrackSample amount)
        {
            var cellSize = settings.TrackCellSize;
            var centerCoordinate = SteppeTrackCellCoordinate.FromWorld(worldX, worldZ, cellSize);
            var radiusInCells = Mathf.CeilToInt((float)(radius / cellSize)) + 1;
            var now = timeSystem.ElapsedSimulationSeconds;

            for (var z = -radiusInCells; z <= radiusInCells; z++)
            {
                for (var x = -radiusInCells; x <= radiusInCells; x++)
                {
                    var coordinate = new SteppeTrackCellCoordinate(centerCoordinate.X + x, centerCoordinate.Z + z);
                    var dx = coordinate.CenterX(cellSize) - worldX;
                    var dz = coordinate.CenterZ(cellSize) - worldZ;
                    var distance = Math.Sqrt(dx * dx + dz * dz);
                    var falloff = Clamp01(1.0 - distance / Math.Max(0.01, radius + cellSize * 0.65));
                    falloff = falloff * falloff * (3.0 - 2.0 * falloff);
                    if (falloff <= 0.001)
                    {
                        continue;
                    }

                    if (!records.TryGetValue(coordinate, out var record))
                    {
                        record = new TrackRecord { LastSimulationSeconds = now };
                        records.Add(coordinate, record);
                    }

                    var existing = Decay(record.Sample, now - record.LastSimulationSeconds);
                    record.Sample = Combine(existing, amount, falloff);
                    record.LastSimulationSeconds = now;
                    WritePixel(coordinate, record.Sample);
                }
            }

            dirty = true;
        }

        private void EnsureMapCoverage()
        {
            var resolution = settings.TrackMapResolution;
            var centerX = mapOrigin.X + resolution / 2;
            var centerZ = mapOrigin.Z + resolution / 2;
            var threshold = resolution / 4;
            if (Math.Abs(center.X - centerX) >= threshold || Math.Abs(center.Z - centerZ) >= threshold)
            {
                RecenterMap();
            }
        }

        private void RecenterMap()
        {
            var resolution = settings.TrackMapResolution;
            mapOrigin = new SteppeTrackCellCoordinate(center.X - resolution / 2, center.Z - resolution / 2);
            hasMapOrigin = true;
            Array.Clear(pixels, 0, pixels.Length);
            var now = timeSystem.ElapsedSimulationSeconds;
            foreach (var pair in records)
            {
                var decayed = Decay(pair.Value.Sample, now - pair.Value.LastSimulationSeconds);
                WritePixel(pair.Key, decayed);
            }

            dirty = true;
            UploadMap();
        }

        private void RefreshVisibleMapAndPrune()
        {
            Array.Clear(pixels, 0, pixels.Length);
            var now = timeSystem.ElapsedSimulationSeconds;
            List<SteppeTrackCellCoordinate> expired = null;
            foreach (var pair in records)
            {
                var decayed = Decay(pair.Value.Sample, now - pair.Value.LastSimulationSeconds);
                var maximum = Math.Max(
                    Math.Max(decayed.VegetationFlattening, decayed.SnowCompression),
                    Math.Max(decayed.SoilRut, decayed.WetPrint));
                if (maximum < 1.0 / 255.0)
                {
                    expired ??= new List<SteppeTrackCellCoordinate>();
                    expired.Add(pair.Key);
                    continue;
                }

                WritePixel(pair.Key, decayed);
            }

            if (expired != null)
            {
                for (var index = 0; index < expired.Count; index++)
                {
                    records.Remove(expired[index]);
                }
            }

            dirty = true;
        }

        private void WritePixel(SteppeTrackCellCoordinate coordinate, SteppeTrackSample sample)
        {
            if (!hasMapOrigin)
            {
                return;
            }

            var x = coordinate.X - mapOrigin.X;
            var z = coordinate.Z - mapOrigin.Z;
            var resolution = settings.TrackMapResolution;
            if (x < 0 || z < 0 || x >= resolution || z >= resolution)
            {
                return;
            }

            pixels[z * resolution + x] = Encode(sample);
        }

        private void UploadMap()
        {
            stateMap.SetPixels32(pixels);
            stateMap.Apply(false, false);
            MapRevision++;
            dirty = false;
            uploadCountdown = settings.TrackMapUploadInterval;

            var originX = mapOrigin.X * (double)settings.TrackCellSize;
            var originZ = mapOrigin.Z * (double)settings.TrackCellSize;
            Shader.SetGlobalTexture(StateMapId, stateMap);
            Shader.SetGlobalVector(MapOriginSizeId, new Vector4(
                PositiveModulo(originX, ShaderCoordinatePeriod),
                PositiveModulo(originZ, ShaderCoordinatePeriod),
                1f / settings.TrackMapWorldSize,
                settings.TrackMapWorldSize));
            Shader.SetGlobalVector(MapParametersId, new Vector4(
                settings.TrackMapResolution,
                1f,
                settings.TrackCellSize,
                (float)ShaderCoordinatePeriod));
            Shader.SetGlobalFloat(RutDepthId, settings.MaximumTrackRutDepth);
        }

        private static SteppeTrackSample Combine(SteppeTrackSample current, SteppeTrackSample amount, double weight)
        {
            return new SteppeTrackSample(
                Accumulate(current.VegetationFlattening, amount.VegetationFlattening * weight),
                Accumulate(current.SnowCompression, amount.SnowCompression * weight),
                Accumulate(current.SoilRut, amount.SoilRut * weight),
                Accumulate(current.WetPrint, amount.WetPrint * weight));
        }

        private static SteppeTrackSample Decay(SteppeTrackSample sample, double elapsedSeconds)
        {
            var elapsed = Math.Max(0.0, elapsedSeconds);
            return new SteppeTrackSample(
                sample.VegetationFlattening * Math.Exp(-elapsed / FlattenRecoverySeconds),
                sample.SnowCompression * Math.Exp(-elapsed / SnowTrackRecoverySeconds),
                sample.SoilRut * Math.Exp(-elapsed / RutRecoverySeconds),
                sample.WetPrint * Math.Exp(-elapsed / WetPrintRecoverySeconds));
        }

        private static double Accumulate(double current, double addition)
        {
            return Clamp01(1.0 - (1.0 - Clamp01(current)) * (1.0 - Clamp01(addition)));
        }

        private static Color32 Encode(SteppeTrackSample sample)
        {
            return new Color32(
                Encode(sample.VegetationFlattening),
                Encode(sample.SnowCompression),
                Encode(sample.SoilRut),
                Encode(sample.WetPrint));
        }

        private static byte Encode(double value)
        {
            return (byte)Math.Round(Clamp01(value) * 255.0);
        }

        private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));

        private static float PositiveModulo(double value, double modulus)
        {
            return (float)(value - Math.Floor(value / modulus) * modulus);
        }

        private void OnDestroy()
        {
            Shader.SetGlobalVector(MapParametersId, Vector4.zero);
            if (stateMap == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(stateMap);
            }
            else
            {
                DestroyImmediate(stateMap);
            }
        }
    }
}
