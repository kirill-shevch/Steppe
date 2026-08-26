using System;
using System.Collections.Generic;
using System.Linq;
using Steppe.Simulation;
using Steppe.UnitySimulation;
using UnityEngine;

namespace Steppe.Rendering
{
    public sealed class NaturalProcessFluxPackDefinition
    {
        public NaturalProcessFluxPackDefinition(string shaderName, SimulationFlux?[] channels)
        {
            ShaderName = shaderName;
            Channels = channels;
        }

        public string ShaderName { get; }
        public IReadOnlyList<SimulationFlux?> Channels { get; }
    }

    public sealed class NaturalProcessVectorDefinition
    {
        public NaturalProcessVectorDefinition(VectorProcess process, string shaderName)
        {
            Process = process;
            ShaderName = shaderName;
        }

        public VectorProcess Process { get; }
        public string ShaderName { get; }
    }

    /// <summary>
    /// Publishes the latest two macro intervals without interpreting them. Flux packs
    /// contain raw accumulated amounts. Vector maps contain XY, net magnitude and
    /// gross magnitude. Presenters own conversion to rates and visual response.
    /// Caravan interventions are intentionally excluded while the caravan is disabled.
    /// </summary>
    [DefaultExecutionOrder(351)]
    [DisallowMultipleComponent]
    public sealed class SteppeNaturalProcessFieldAtlas : MonoBehaviour
    {
        private static readonly int BoundsId =
            Shader.PropertyToID("_SteppeNaturalProcessMapBounds");
        private static readonly int GridId =
            Shader.PropertyToID("_SteppeNaturalProcessGrid");
        private static readonly int BlendId =
            Shader.PropertyToID("_SteppeNaturalProcessBlend");
        private static readonly int ReadyId =
            Shader.PropertyToID("_SteppeNaturalProcessReady");
        private static readonly int PeriodHoursId =
            Shader.PropertyToID("_SteppeNaturalProcessPeriodHours");

        private static readonly SimulationFlux[] NaturalFluxIds = FluxCatalog.All
            .Where(descriptor => descriptor.Order < 600)
            .Select(descriptor => descriptor.Id)
            .ToArray();
        private static readonly NaturalProcessFluxPackDefinition[] FluxPackDefinitions =
            BuildFluxPackDefinitions();
        private static readonly NaturalProcessVectorDefinition[] VectorDefinitions =
            VectorProcessCatalog.All
                .Select(descriptor => new NaturalProcessVectorDefinition(
                    descriptor.Process,
                    descriptor.Process.ToString()))
                .ToArray();

        private SteppeSimulationHost host;
        private Texture2D[] fluxFromMaps;
        private Texture2D[] fluxToMaps;
        private Color[][] fluxFromPixels;
        private Color[][] fluxToPixels;
        private int[] fluxFromIds;
        private int[] fluxToIds;
        private Texture2D[] vectorFromMaps;
        private Texture2D[] vectorToMaps;
        private Color[][] vectorFromPixels;
        private Color[][] vectorToPixels;
        private int[] vectorFromIds;
        private int[] vectorToIds;
        private SteppeSimulationSnapshot lastFrom;
        private SteppeSimulationSnapshot lastTo;

        public static IReadOnlyList<SimulationFlux> NaturalFluxes { get; } =
            Array.AsReadOnly(NaturalFluxIds);
        public static IReadOnlyList<NaturalProcessFluxPackDefinition> FluxPacks { get; } =
            Array.AsReadOnly(FluxPackDefinitions);
        public static IReadOnlyList<NaturalProcessVectorDefinition> Vectors { get; } =
            Array.AsReadOnly(VectorDefinitions);

        public bool IsReady { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int FluxCount => NaturalFluxIds.Length;
        public int VectorCount => VectorDefinitions.Length;

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
                if (!BuildSnapshot(
                        previous,
                        fluxFromMaps,
                        fluxFromPixels,
                        vectorFromMaps,
                        vectorFromPixels)
                    || !BuildSnapshot(
                        latest,
                        fluxToMaps,
                        fluxToPixels,
                        vectorToMaps,
                        vectorToPixels))
                {
                    SetReady(false);
                    return;
                }

                lastFrom = previous;
                lastTo = latest;
            }

            Shader.SetGlobalFloat(BlendId, Mathf.Clamp01(host.MacroInterpolationAlpha));
            Shader.SetGlobalVector(PeriodHoursId, new Vector4(
                GetPeriodHours(previous),
                GetPeriodHours(latest),
                0f,
                0f));
            SetReady(true);
        }

        private static float GetPeriodHours(SteppeSimulationSnapshot snapshot)
        {
            if (snapshot != null
                && NaturalFluxIds.Length > 0
                && snapshot.TryGetFlux(NaturalFluxIds[0], out var flux))
            {
                return Mathf.Max(0.0001f, (float)flux.PeriodHours);
            }

            return 1f;
        }

        private void EnsureTextures(SteppeSimulationSnapshot snapshot)
        {
            var width = snapshot.Summary.Width;
            var height = snapshot.Summary.Height;
            if (fluxFromMaps == null || Width != width || Height != height)
            {
                ReleaseTextures();
                Width = width;
                Height = height;
                CreateTextureSet(
                    FluxPackDefinitions.Select(definition => definition.ShaderName).ToArray(),
                    "Flux",
                    width,
                    height,
                    out fluxFromMaps,
                    out fluxToMaps,
                    out fluxFromPixels,
                    out fluxToPixels,
                    out fluxFromIds,
                    out fluxToIds);
                CreateTextureSet(
                    VectorDefinitions.Select(definition => definition.ShaderName).ToArray(),
                    "Vector",
                    width,
                    height,
                    out vectorFromMaps,
                    out vectorToMaps,
                    out vectorFromPixels,
                    out vectorToPixels,
                    out vectorFromIds,
                    out vectorToIds);
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

        private static void CreateTextureSet(
            string[] names,
            string prefix,
            int width,
            int height,
            out Texture2D[] fromMaps,
            out Texture2D[] toMaps,
            out Color[][] fromPixels,
            out Color[][] toPixels,
            out int[] fromIds,
            out int[] toIds)
        {
            fromMaps = new Texture2D[names.Length];
            toMaps = new Texture2D[names.Length];
            fromPixels = new Color[names.Length][];
            toPixels = new Color[names.Length][];
            fromIds = new int[names.Length];
            toIds = new int[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                fromMaps[index] = CreateMap(
                    $"Steppe natural {prefix.ToLowerInvariant()} {names[index]} previous",
                    width,
                    height);
                toMaps[index] = CreateMap(
                    $"Steppe natural {prefix.ToLowerInvariant()} {names[index]} latest",
                    width,
                    height);
                fromPixels[index] = new Color[width * height];
                toPixels[index] = new Color[width * height];
                fromIds[index] = Shader.PropertyToID(
                    $"_SteppeNatural{prefix}{names[index]}From");
                toIds[index] = Shader.PropertyToID(
                    $"_SteppeNatural{prefix}{names[index]}To");
                Shader.SetGlobalTexture(fromIds[index], fromMaps[index]);
                Shader.SetGlobalTexture(toIds[index], toMaps[index]);
            }
        }

        private static bool BuildSnapshot(
            SteppeSimulationSnapshot snapshot,
            Texture2D[] fluxMaps,
            Color[][] fluxPixels,
            Texture2D[] vectorMaps,
            Color[][] vectorPixels)
        {
            for (var packIndex = 0; packIndex < FluxPackDefinitions.Length; packIndex++)
            {
                var definition = FluxPackDefinitions[packIndex];
                var sources = new FluxSnapshot[4];
                for (var channel = 0; channel < 4; channel++)
                {
                    var flux = definition.Channels[channel];
                    if (flux.HasValue && !snapshot.TryGetFlux(flux.Value, out sources[channel]))
                    {
                        return false;
                    }
                }

                var target = fluxPixels[packIndex];
                for (var pixelIndex = 0; pixelIndex < target.Length; pixelIndex++)
                {
                    target[pixelIndex] = PackRaw(
                        ValueAt(sources[0], pixelIndex),
                        ValueAt(sources[1], pixelIndex),
                        ValueAt(sources[2], pixelIndex),
                        ValueAt(sources[3], pixelIndex));
                }

                fluxMaps[packIndex].SetPixels(target);
                fluxMaps[packIndex].Apply(false, false);
            }

            for (var vectorIndex = 0; vectorIndex < VectorDefinitions.Length; vectorIndex++)
            {
                if (!snapshot.TryGetVectorProcess(
                        VectorDefinitions[vectorIndex].Process,
                        out var source))
                {
                    return false;
                }

                var target = vectorPixels[vectorIndex];
                if (source.VectorX.Length != target.Length
                    || source.VectorY.Length != target.Length
                    || source.Magnitude.Length != target.Length
                    || source.GrossMagnitude.Length != target.Length)
                {
                    return false;
                }

                for (var pixelIndex = 0; pixelIndex < target.Length; pixelIndex++)
                {
                    target[pixelIndex] = PackRaw(
                        source.VectorX[pixelIndex],
                        source.VectorY[pixelIndex],
                        source.Magnitude[pixelIndex],
                        source.GrossMagnitude[pixelIndex]);
                }

                vectorMaps[vectorIndex].SetPixels(target);
                vectorMaps[vectorIndex].Apply(false, false);
            }

            return true;
        }

        private static float ValueAt(FluxSnapshot source, int index) =>
            source != null && index < source.Values.Length ? source.Values[index] : 0f;

        public static Color PackRaw(float red, float green, float blue, float alpha) =>
            new Color(Sanitize(red), Sanitize(green), Sanitize(blue), Sanitize(alpha));

        private static float Sanitize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        private static NaturalProcessFluxPackDefinition[] BuildFluxPackDefinitions()
        {
            var count = Mathf.CeilToInt(NaturalFluxIds.Length / 4f);
            var result = new NaturalProcessFluxPackDefinition[count];
            for (var packIndex = 0; packIndex < count; packIndex++)
            {
                var channels = new SimulationFlux?[4];
                for (var channel = 0; channel < 4; channel++)
                {
                    var fluxIndex = packIndex * 4 + channel;
                    if (fluxIndex < NaturalFluxIds.Length)
                    {
                        channels[channel] = NaturalFluxIds[fluxIndex];
                    }
                }

                result[packIndex] = new NaturalProcessFluxPackDefinition(
                    packIndex.ToString("00"),
                    channels);
            }

            return result;
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
            ReleaseTextureSet(fluxFromMaps, fluxToMaps, fluxFromIds, fluxToIds);
            ReleaseTextureSet(vectorFromMaps, vectorToMaps, vectorFromIds, vectorToIds);
            fluxFromMaps = null;
            fluxToMaps = null;
            fluxFromPixels = null;
            fluxToPixels = null;
            fluxFromIds = null;
            fluxToIds = null;
            vectorFromMaps = null;
            vectorToMaps = null;
            vectorFromPixels = null;
            vectorToPixels = null;
            vectorFromIds = null;
            vectorToIds = null;
            Width = 0;
            Height = 0;
        }

        private static void ReleaseTextureSet(
            Texture2D[] fromMaps,
            Texture2D[] toMaps,
            int[] fromIds,
            int[] toIds)
        {
            if (fromMaps == null)
            {
                return;
            }

            for (var index = 0; index < fromMaps.Length; index++)
            {
                if (fromIds != null)
                {
                    Shader.SetGlobalTexture(fromIds[index], null);
                    Shader.SetGlobalTexture(toIds[index], null);
                }

                DestroyTexture(fromMaps[index]);
                DestroyTexture(toMaps[index]);
            }
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
            Shader.SetGlobalVector(PeriodHoursId, Vector4.one);
        }
    }
}
