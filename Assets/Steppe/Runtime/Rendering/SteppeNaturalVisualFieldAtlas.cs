using System;
using System.Collections.Generic;
using Steppe.Simulation;
using Steppe.UnitySimulation;
using UnityEngine;

namespace Steppe.Rendering
{
    /// <summary>
    /// One stable four-channel GPU pack. The channels carry raw simulation units;
    /// presenters own normalization because each physical carrier has a different
    /// useful response curve.
    /// </summary>
    public sealed class NaturalVisualPackDefinition
    {
        public NaturalVisualPackDefinition(
            string shaderName,
            SimulationLayer red,
            SimulationLayer green,
            SimulationLayer blue,
            SimulationLayer? alpha)
        {
            ShaderName = shaderName;
            Channels = new[] { red, green, blue, alpha };
        }

        public string ShaderName { get; }
        public IReadOnlyList<SimulationLayer?> Channels { get; }
    }

    /// <summary>
    /// Publishes all finite-world state as raw, bilinearly sampled semantic fields.
    /// Previous and latest macro snapshots stay separate on the GPU; only the blend
    /// value changes every rendered frame. Natural presenters therefore see a
    /// continuous world while the fixed simulation boundary remains an internal
    /// implementation detail.
    /// </summary>
    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    public sealed class SteppeNaturalVisualFieldAtlas : MonoBehaviour
    {
        private static readonly int BoundsId =
            Shader.PropertyToID("_SteppeNaturalVisualMapBounds");
        private static readonly int BlendId =
            Shader.PropertyToID("_SteppeNaturalVisualBlend");
        private static readonly int ReadyId =
            Shader.PropertyToID("_SteppeNaturalVisualReady");
        private static readonly int GridId =
            Shader.PropertyToID("_SteppeNaturalVisualGrid");
        private static readonly int DrainageFromId =
            Shader.PropertyToID("_SteppeNaturalDrainageFrom");
        private static readonly int DrainageToId =
            Shader.PropertyToID("_SteppeNaturalDrainageTo");

        private static readonly NaturalVisualPackDefinition[] PackDefinitions =
        {
            Pack("TerrainA", SimulationLayer.Elevation, SimulationLayer.Slope,
                SimulationLayer.Aspect, SimulationLayer.SoilDepth),
            Pack("TerrainB", SimulationLayer.FaultInfluence, SimulationLayer.RockHardness,
                SimulationLayer.DepressionStorage, SimulationLayer.Drainage),
            Pack("SoilA", SimulationLayer.SandFraction, SimulationLayer.SiltFraction,
                SimulationLayer.ClayFraction, SimulationLayer.Porosity),
            Pack("SoilB", SimulationLayer.Permeability, SimulationLayer.MineralContent,
                SimulationLayer.SoilCompaction, SimulationLayer.RootWater),
            Pack("AirA", SimulationLayer.SolarRadiation, SimulationLayer.SurfaceTemperature,
                SimulationLayer.SoilTemperature, SimulationLayer.AirTemperature),
            Pack("AirB", SimulationLayer.Pressure, SimulationLayer.Wind,
                SimulationLayer.Humidity, SimulationLayer.CloudWater),
            Pack("WaterA", SimulationLayer.Precipitation, SimulationLayer.SurfaceWater,
                SimulationLayer.Groundwater, SimulationLayer.Snow),
            Pack("LifeA", SimulationLayer.FrozenSoil, SimulationLayer.LiveBiomass,
                SimulationLayer.DryBiomass, SimulationLayer.LitterBiomass),
            Pack("LifeB", SimulationLayer.SeedBank, SimulationLayer.SoilOrganicMatter,
                SimulationLayer.AvailableNitrogen, SimulationLayer.PlantNitrogen),
            Pack("MaterialA", SimulationLayer.OrganicNitrogen, SimulationLayer.LooseSediment,
                SimulationLayer.SurfaceCrust, SimulationLayer.Dust),
            Pack("ProcessA", SimulationLayer.FireIntensity, SimulationLayer.BurnScar,
                SimulationLayer.Catchments),
        };

        private SteppeSimulationHost host;
        private Texture2D[] fromMaps;
        private Texture2D[] toMaps;
        private Color[][] fromPixels;
        private Color[][] toPixels;
        private int[] fromShaderIds;
        private int[] toShaderIds;
        private Texture2D drainageFromMap;
        private Texture2D drainageToMap;
        private Color[] drainageFromPixels;
        private Color[] drainageToPixels;
        private SteppeSimulationSnapshot lastFrom;
        private SteppeSimulationSnapshot lastTo;

        public static IReadOnlyList<NaturalVisualPackDefinition> Packs { get; } =
            Array.AsReadOnly(PackDefinitions);

        public bool IsReady { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public void Configure(SteppeSimulationHost simulationHost)
        {
            host = simulationHost != null
                ? simulationHost
                : throw new ArgumentNullException(nameof(simulationHost));
            Shader.SetGlobalFloat(ReadyId, 0f);
            Shader.SetGlobalFloat(BlendId, 0f);
        }

        private void Update()
        {
            var latest = host != null ? host.LatestSnapshot : null;
            if (latest == null)
            {
                SetReady(false);
                return;
            }

            var previous = host.PreviousSnapshot ?? latest;
            EnsureTextures(latest);
            if (!ReferenceEquals(previous, lastFrom) || !ReferenceEquals(latest, lastTo))
            {
                if (!BuildSnapshot(previous, fromMaps, fromPixels)
                    || !BuildSnapshot(latest, toMaps, toPixels)
                    || !BuildDrainageSnapshot(previous, drainageFromMap, drainageFromPixels)
                    || !BuildDrainageSnapshot(latest, drainageToMap, drainageToPixels))
                {
                    SetReady(false);
                    return;
                }

                lastFrom = previous;
                lastTo = latest;
            }

            Shader.SetGlobalFloat(BlendId, Mathf.Clamp01(host.MacroInterpolationAlpha));
            SetReady(true);
        }

        private void EnsureTextures(SteppeSimulationSnapshot snapshot)
        {
            var width = snapshot.Summary.Width;
            var height = snapshot.Summary.Height;
            if (fromMaps == null || Width != width || Height != height)
            {
                ReleaseTextures();
                Width = width;
                Height = height;
                var count = PackDefinitions.Length;
                fromMaps = new Texture2D[count];
                toMaps = new Texture2D[count];
                fromPixels = new Color[count][];
                toPixels = new Color[count][];
                fromShaderIds = new int[count];
                toShaderIds = new int[count];
                for (var index = 0; index < count; index++)
                {
                    var definition = PackDefinitions[index];
                    fromMaps[index] = CreateMap($"Steppe {definition.ShaderName} previous", width, height);
                    toMaps[index] = CreateMap($"Steppe {definition.ShaderName} latest", width, height);
                    fromPixels[index] = new Color[width * height];
                    toPixels[index] = new Color[width * height];
                    fromShaderIds[index] = Shader.PropertyToID(
                        $"_SteppeNatural{definition.ShaderName}From");
                    toShaderIds[index] = Shader.PropertyToID(
                        $"_SteppeNatural{definition.ShaderName}To");
                    Shader.SetGlobalTexture(fromShaderIds[index], fromMaps[index]);
                    Shader.SetGlobalTexture(toShaderIds[index], toMaps[index]);
                }

                drainageFromMap = CreateMap("Steppe drainage topology previous", width, height);
                drainageToMap = CreateMap("Steppe drainage topology latest", width, height);
                drainageFromPixels = new Color[width * height];
                drainageToPixels = new Color[width * height];
                Shader.SetGlobalTexture(DrainageFromId, drainageFromMap);
                Shader.SetGlobalTexture(DrainageToId, drainageToMap);

                lastFrom = null;
                lastTo = null;
            }

            var coordinates = snapshot.Coordinates;
            Shader.SetGlobalVector(BoundsId, new Vector4(
                (float)coordinates.MinimumWorldX,
                (float)coordinates.MinimumWorldZ,
                1f / (coordinates.Width * coordinates.CellSizeMeters),
                1f / (coordinates.Height * coordinates.CellSizeMeters)));
            Shader.SetGlobalVector(GridId, new Vector4(
                coordinates.Width,
                coordinates.Height,
                (float)coordinates.CellSizeMeters,
                1f / (float)coordinates.CellSizeMeters));
        }

        private static bool BuildSnapshot(
            SteppeSimulationSnapshot snapshot,
            Texture2D[] maps,
            Color[][] pixels)
        {
            for (var packIndex = 0; packIndex < PackDefinitions.Length; packIndex++)
            {
                var definition = PackDefinitions[packIndex];
                if (!TryGetLayer(snapshot, definition.Channels[0], out var red)
                    || !TryGetLayer(snapshot, definition.Channels[1], out var green)
                    || !TryGetLayer(snapshot, definition.Channels[2], out var blue)
                    || !TryGetLayer(snapshot, definition.Channels[3], out var alpha))
                {
                    return false;
                }

                var target = pixels[packIndex];
                if (red.Values.Length != target.Length
                    || green.Values.Length != target.Length
                    || blue.Values.Length != target.Length
                    || (alpha != null && alpha.Values.Length != target.Length))
                {
                    return false;
                }

                for (var pixelIndex = 0; pixelIndex < target.Length; pixelIndex++)
                {
                    target[pixelIndex] = PackRaw(
                        red.Values[pixelIndex],
                        green.Values[pixelIndex],
                        blue.Values[pixelIndex],
                        alpha != null ? alpha.Values[pixelIndex] : 0f);
                }

                maps[packIndex].SetPixels(target);
                maps[packIndex].Apply(false, false);
            }

            return true;
        }

        private static bool BuildDrainageSnapshot(
            SteppeSimulationSnapshot snapshot,
            Texture2D map,
            Color[] pixels)
        {
            if (!snapshot.TryGetLayer(SimulationLayer.Drainage, out var drainage)
                || !snapshot.TryGetLayer(SimulationLayer.Catchments, out var catchments)
                || drainage.VectorX == null
                || drainage.VectorY == null
                || drainage.Width != catchments.Width
                || drainage.Height != catchments.Height
                || drainage.VectorX.Length != pixels.Length
                || drainage.VectorY.Length != pixels.Length
                || catchments.Values.Length != pixels.Length)
            {
                return false;
            }

            BuildDrainageTopology(drainage, catchments, pixels);
            map.SetPixels(pixels);
            map.Apply(false, false);
            return true;
        }

        /// <summary>
        /// Converts the authoritative next-cell vectors into render-only topology.
        /// B stores log2(1 + contributing cells); A stores Strahler branch order.
        /// No flow rule is inferred here: every edge comes directly from DrainTo and
        /// is accepted only inside the catchment id assigned by the simulation.
        /// </summary>
        public static void BuildDrainageTopology(
            LayerSnapshot drainage,
            LayerSnapshot catchments,
            Color[] target)
        {
            if (drainage == null) throw new ArgumentNullException(nameof(drainage));
            if (catchments == null) throw new ArgumentNullException(nameof(catchments));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (drainage.VectorX == null || drainage.VectorY == null)
            {
                throw new ArgumentException("Drainage snapshot has no vector field.", nameof(drainage));
            }

            var count = drainage.Width * drainage.Height;
            if (drainage.VectorX.Length != count
                || drainage.VectorY.Length != count
                || catchments.Values.Length != count
                || target.Length != count)
            {
                throw new ArgumentException("Drainage topology raster sizes do not match.");
            }

            var targets = new int[count];
            var incoming = new int[count];
            var remainingIncoming = new int[count];
            var contributing = new float[count];
            var branchOrder = new int[count];
            var maximumIncomingOrder = new int[count];
            var maximumIncomingOrderCount = new int[count];
            for (var index = 0; index < count; index++)
            {
                targets[index] = -1;
                contributing[index] = 1f;
                branchOrder[index] = 1;
                var dx = Math.Sign(drainage.VectorX[index]);
                var dy = Math.Sign(drainage.VectorY[index]);
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var x = index % drainage.Width;
                var y = index / drainage.Width;
                var targetX = x + dx;
                var targetY = y + dy;
                if (targetX < 0 || targetY < 0
                    || targetX >= drainage.Width || targetY >= drainage.Height)
                {
                    continue;
                }

                var next = targetY * drainage.Width + targetX;
                if (next == index
                    || Mathf.RoundToInt(catchments.Values[next])
                    != Mathf.RoundToInt(catchments.Values[index]))
                {
                    continue;
                }

                targets[index] = next;
                incoming[next]++;
                remainingIncoming[next]++;
            }

            var queue = new Queue<int>(count);
            for (var index = 0; index < count; index++)
            {
                if (incoming[index] == 0)
                {
                    queue.Enqueue(index);
                }
            }

            var visited = 0;
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                visited++;
                var next = targets[index];
                if (next < 0)
                {
                    continue;
                }

                contributing[next] += contributing[index];
                var order = branchOrder[index];
                if (order > maximumIncomingOrder[next])
                {
                    maximumIncomingOrder[next] = order;
                    maximumIncomingOrderCount[next] = 1;
                }
                else if (order == maximumIncomingOrder[next])
                {
                    maximumIncomingOrderCount[next]++;
                }

                remainingIncoming[next]--;
                if (remainingIncoming[next] == 0)
                {
                    branchOrder[next] = Math.Max(
                        1,
                        maximumIncomingOrder[next]
                        + (maximumIncomingOrderCount[next] > 1 ? 1 : 0));
                    queue.Enqueue(next);
                }
            }

            // Priority-flood drainage is acyclic. This fallback only prevents corrupt
            // imported data from publishing NaNs or uninitialized topology to shaders.
            if (visited < count)
            {
                for (var index = 0; index < count; index++)
                {
                    contributing[index] = Mathf.Max(1f, contributing[index]);
                    branchOrder[index] = Math.Max(1, branchOrder[index]);
                }
            }

            for (var index = 0; index < count; index++)
            {
                target[index] = PackRaw(
                    drainage.VectorX[index],
                    drainage.VectorY[index],
                    Mathf.Log(1f + contributing[index], 2f),
                    branchOrder[index]);
            }
        }

        public static Color PackRaw(float red, float green, float blue, float alpha) =>
            new Color(Sanitize(red), Sanitize(green), Sanitize(blue), Sanitize(alpha));

        private static float Sanitize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        private static NaturalVisualPackDefinition Pack(
            string name,
            SimulationLayer red,
            SimulationLayer green,
            SimulationLayer blue,
            SimulationLayer? alpha = null) =>
            new NaturalVisualPackDefinition(name, red, green, blue, alpha);

        private static bool TryGetLayer(
            SteppeSimulationSnapshot snapshot,
            SimulationLayer? layer,
            out LayerSnapshot values)
        {
            if (layer.HasValue)
            {
                return snapshot.TryGetLayer(layer.Value, out values);
            }

            values = null;
            return true;
        }

        private static Texture2D CreateMap(string mapName, int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
            {
                name = mapName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

        private void SetReady(bool ready)
        {
            IsReady = ready;
            Shader.SetGlobalFloat(ReadyId, ready ? 1f : 0f);
        }

        private void ReleaseTextures()
        {
            SetReady(false);
            if (fromMaps != null)
            {
                for (var index = 0; index < fromMaps.Length; index++)
                {
                    if (fromShaderIds != null)
                    {
                        Shader.SetGlobalTexture(fromShaderIds[index], null);
                        Shader.SetGlobalTexture(toShaderIds[index], null);
                    }

                    DestroyTexture(fromMaps[index]);
                    DestroyTexture(toMaps[index]);
                }
            }

            fromMaps = null;
            toMaps = null;
            fromPixels = null;
            toPixels = null;
            fromShaderIds = null;
            toShaderIds = null;
            Shader.SetGlobalTexture(DrainageFromId, null);
            Shader.SetGlobalTexture(DrainageToId, null);
            DestroyTexture(drainageFromMap);
            DestroyTexture(drainageToMap);
            drainageFromMap = null;
            drainageToMap = null;
            drainageFromPixels = null;
            drainageToPixels = null;
            Width = 0;
            Height = 0;
        }

        private static void DestroyTexture(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private void OnDestroy()
        {
            ReleaseTextures();
            Shader.SetGlobalFloat(BlendId, 0f);
        }
    }
}
